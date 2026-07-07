using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Shared.Services;

namespace MPM.Modules.Analisis.Services;

/// <summary>
/// Cliente de Gemini vía Vertex AI, autenticado con ADC (020-migracion-gemini-adc) — ya no vía
/// API key. Vertex AI no tiene la "File API" efímera de la Developer API
/// (<c>generativelanguage.googleapis.com/upload/...</c>), así que el bug ya conocido de PDFs
/// escaneados (ver memoria "feedback-scraper-bugs" Bug 3) se resuelve distinto acá: cuando el
/// documento ya vive en GCS (<c>Storage:Provider=gcs</c>, el caso de producción), se referencia
/// directo por <c>fileData.fileUri = gs://...</c> — Gemini lee el PDF completo desde GCS, sin
/// las limitaciones de tamaño/calidad de mandar el PDF inline en base64. Con storage local se
/// usa <c>inlineData</c> (mismo fallback que ya existía).
/// </summary>
public class GeminiService(HttpClient httpClient, IConfiguration config, GoogleAdcTokenProvider tokenProvider, ILogger<GeminiService> logger)
{
    public const string ModelName = "gemini-2.5-pro";
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string ProjectId => config["GOOGLE_CLOUD_PROJECT"]
        ?? throw new InvalidOperationException("GOOGLE_CLOUD_PROJECT no configurado");
    private string Region => config["Vertex:Region"] ?? "us-central1";

    private string EndpointFor(string model) =>
        $"https://{Region}-aiplatform.googleapis.com/v1/projects/{ProjectId}/locations/{Region}/publishers/google/models/{model}:generateContent";

    private async Task<HttpRequestMessage> BuildRequestAsync(string model, object body, CancellationToken ct)
    {
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, EndpointFor(model))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    /// <param name="gcsUri">
    /// Si el documento ya está en GCS (<c>gs://...</c>), se referencia directo sin mandar
    /// <paramref name="pdfBytes"/> en el body — más eficiente y evita el límite de tamaño de
    /// <c>inlineData</c>. Si es <c>null</c> (storage local), se usa <paramref name="pdfBytes"/>
    /// como <c>inlineData</c> en base64.
    /// </param>
    public async Task<GeminiResponse> AnalyzePdfAsync(byte[] pdfBytes, string fileName, string? gcsUri, CancellationToken ct = default)
    {
        var prompt = GetAnalisisPrompt();
        logger.LogInformation("Enviando PDF {File} a Gemini (Vertex AI) para análisis ({Size} bytes, gcsUri={GcsUri})", fileName, pdfBytes.Length, gcsUri ?? "(inline)");

        object documentPart = gcsUri != null
            ? new { fileData = new { mimeType = "application/pdf", fileUri = gcsUri } }
            : new { inlineData = new { mimeType = "application/pdf", data = Convert.ToBase64String(pdfBytes) } };

        var request = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { documentPart, new { text = prompt } }
                }
            },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 65536, responseMimeType = "application/json" }
        };

        using var httpRequest = await BuildRequestAsync(ModelName, request, ct);
        var response = await httpClient.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Gemini (Vertex AI) error {Status}: {Body}", (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var jsonResponse = await response.Content.ReadAsStringAsync(ct);
        logger.LogInformation("Respuesta de Gemini recibida ({Length} chars)", jsonResponse.Length);
        return await ParseGeminiResponse(jsonResponse);
    }

    public async Task<GeminiChatResponse> ChatAsync(string mensaje, string contextoAnalisis, List<ChatHistoryItem> historial, CancellationToken ct = default)
    {
        var systemInstruction = $@"Eres un asistente experto en análisis de licitaciones.
Contexto del análisis (JSON):
{contextoAnalisis}

Responde las consultas del usuario basándote exclusivamente en este análisis.
Sé breve, claro y ejecutivo en tus respuestas.

FORMATO DE RESPUESTA (obligatorio):
- Responde SIEMPRE en Markdown válido y bien formado.
- NUNCA envuelvas la respuesta en fences de código (``` o ```json o ```markdown).
- Usa listas con guiones, negritas con ** y tablas Markdown estándar cuando presentes datos comparativos.
- No uses HTML.";

        var contents = new List<object>();

        foreach (var h in historial.TakeLast(20))
        {
            var geminiRole = h.Rol == "assistant" ? "model" : h.Rol;
            contents.Add(new { role = geminiRole, parts = new[] { new { text = h.Contenido } } });
        }

        // Only add the current message if it's not already the last in history
        var lastHistorial = historial.LastOrDefault();
        if (lastHistorial == null || lastHistorial.Contenido != mensaje)
        {
            contents.Add(new { role = "user", parts = new[] { new { text = mensaje } } });
        }

        var request = new
        {
            systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
            contents,
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 2048
            }
        };

        using var httpRequest = await BuildRequestAsync(ModelName, request, ct);
        var response = await httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync(ct);
        return await ParseChatResponse(jsonResponse);
    }

    private static async Task<GeminiResponse> ParseGeminiResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var text = "";
        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                text = parts[0].GetProperty("text").GetString() ?? "";
            }
        }

        // Strip markdown code fences if Gemini wraps JSON in ```json ... ```
        if (text.StartsWith("```"))
        {
            var newline = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```");
            if (newline >= 0 && lastFence > newline)
                text = text[(newline + 1)..lastFence].Trim();
        }
        // Ensure text starts at first '{' or '[' (remove any preamble)
        var jsonStart = text.IndexOfAny(['{', '[']);
        if (jsonStart > 0)
            text = text[jsonStart..];

        var usage = new GeminiUsage();
        if (root.TryGetProperty("usageMetadata", out var usageMeta))
        {
            usage.PromptTokenCount = usageMeta.TryGetProperty("promptTokenCount", out var ptc) ? ptc.GetInt32() : 0;
            usage.CandidatesTokenCount = usageMeta.TryGetProperty("candidatesTokenCount", out var ctc) ? ctc.GetInt32() : 0;
            usage.TotalTokenCount = usageMeta.TryGetProperty("totalTokenCount", out var ttc) ? ttc.GetInt32() : 0;
        }

        return new GeminiResponse
        {
            Text = text,
            Usage = usage,
            RawResponse = json
        };
    }

    private static async Task<GeminiChatResponse> ParseChatResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var text = "";
        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                text = parts[0].GetProperty("text").GetString() ?? "";
            }
        }

        var finishReason = "";
        if (candidates.GetArrayLength() > 0 && candidates[0].TryGetProperty("finishReason", out var fr))
        {
            finishReason = fr.GetString() ?? "";
        }

        return new GeminiChatResponse
        {
            Text = text,
            FinishReason = finishReason
        };
    }

    private static string GetAnalisisPrompt()
    {
        return @"Eres un analista experto en licitaciones públicas chilenas (Ley 19.886). Analiza TÉCNICAMENTE el siguiente documento de evaluación de licitación y extrae TODA la información relevante estructurada en el siguiente JSON. No omitas ningún campo disponible.

REGLAS GENERALES:
- Extrae SOLO información explícitamente presente en el documento
- Si un dato no está disponible, usa null (nunca inventes)
- Textos siempre en español, formato claro y analítico
- Los montos deben ser numéricos (sin puntos ni separadores)
- Las fechas en formato YYYY-MM-DD
- Sé exhaustivo: cada criterio, subcriterio, ponderación y puntaje debe capturarse individualmente

{
  ""licitacion"": {
    ""id"": ""Código o ID de la licitación (ej: 622-11-I226)"",
    ""nombre"": ""Nombre completo de la licitación"",
    ""descripcion"": ""Descripción del objeto contractual (máx 500 chars)"",
    ""organismo"": {
      ""nombre"": ""Nombre del organismo público convocante"",
      ""rut"": ""RUT del organismo"",
      ""unidad"": ""Unidad de compra (si aplica)"",
      ""region"": ""Región del organismo""
    },
    ""tipo_licitacion"": ""Tipo: Pública/Privada/Convenio Marco/etc"",
    ""tipo_convocatoria"": ""Abierto/Cerrado"",
    ""codigo_etapa"": ""Código de etapa (LE, LP, LR, B2, CO, etc)"",
    ""estado"": ""Estado: Adjudicada/Desierta/Revocada"",
    ""moneda"": ""CLP/UF/USD/EUR"",
    ""fechas"": {
      ""publicacion"": ""YYYY-MM-DD"",
      ""cierre_ofertas"": ""YYYY-MM-DD"",
      ""apertura_tecnica"": ""YYYY-MM-DD o null"",
      ""apertura_economica"": ""YYYY-MM-DD o null"",
      ""adjudicacion"": ""YYYY-MM-DD o null""
    },
    ""monto_estimado"": 0.0,
    ""duracion_contrato"": ""Ej: 36 Meses"",
    ""renovacion"": ""SI/NO o null"",
    ""toma_razon_contraloria"": ""SI/NO o null"",
    ""prohibicion_subcontratacion"": ""SI/NO o null"",
    ""plazo_pago"": ""Ej: 30 días contra factura conforme""
  },
  ""adjudicacion"": {
    ""adjudicatario"": {
      ""nombre"": ""Nombre del proveedor adjudicado"",
      ""rut"": ""RUT del adjudicatario"",
      ""monto_adjudicado"": 0.0,
      ""cantidad_ofertas_recibidas"": 0
    },
    ""ofertantes"": [
      {
        ""nombre"": ""Nombre del ofertante"",
        ""rut"": ""RUT del ofertante"",
        ""monto_ofertado"": 0.0,
        ""puntaje_total"": 0.0,
        ""resultado"": ""Adjudicado/No adjudicado/Inadmisible/Desistido"",
        ""motivo_inadmisibilidad"": ""Si aplica, texto del motivo (null si no)""
      }
    ]
  },
  ""evaluacion"": {
    ""metodologia"": ""Descripción de la metodología de evaluación utilizada"",
    ""criterios"": [
      {
        ""nombre"": ""Nombre del criterio (Ej: Técnico, Económico, Administrativo)"",
        ""ponderacion"": 0.0,
        ""subcriterios"": [
          {
            ""nombre"": ""Nombre del subcriterio"",
            ""ponderacion"": 0.0,
            ""descripcion"": ""Qué evalúa este subcriterio (máx 300 chars)"",
            ""puntaje_maximo"": 0.0,
            ""puntaje_tivit"": 0.0,
            ""puntaje_ganador"": 0.0,
            ""comentario_evaluacion"": ""Comentario del evaluador sobre este criterio (null si no disponible)""
          }
        ],
        ""puntaje_maximo_total"": 0.0,
        ""puntaje_tivit_total"": 0.0,
        ""puntaje_ganador_total"": 0.0,
        ""brecha"": 0.0
      }
    ],
    ""desglose_puntajes"": {
      ""tivit"": { ""puntaje_tecnico"": 0.0, ""puntaje_economico"": 0.0, ""puntaje_administrativo"": 0.0, ""puntaje_total"": 0.0, ""porcentaje_cumplimiento"": 0.0 },
      ""ganador"": { ""puntaje_tecnico"": 0.0, ""puntaje_economico"": 0.0, ""puntaje_administrativo"": 0.0, ""puntaje_total"": 0.0, ""porcentaje_cumplimiento"": 0.0 }
    }
  },
  ""requisitos"": {
    ""antecedentes_requeridos"": [""Lista de documentos solicitados para ofertar""],
    ""requisitos_contratacion"": [""Requisitos para contratar al adjudicado""],
    ""garantias"": [
      {
        ""tipo"": ""Ej: Garantía fiel de Cumplimiento de Contrato"",
        ""porcentaje"": 0.0,
        ""monto"": 0.0,
        ""beneficiario"": ""Nombre del beneficiario"",
        ""fecha_vencimiento"": ""YYYY-MM-DD o null""
      }
    ]
  },
  ""analisis_tivit"": {
    ""participa"": true,
    ""es_ganador"": false,
    ""monto_ofertado"": 0.0,
    ""puntaje_obtenido"": 0.0,
    ""puntaje_maximo_posible"": 0.0,
    ""resultado"": ""Adjudicado/No adjudicado/Inadmisible"",
    ""fortalezas"": [""Aspectos donde TIVIT obtuvo mejor evaluación que el promedio""],
    ""debilidades"": [""Aspectos donde TIVIT obtuvo menor puntaje o fue superado""],
    ""brechas_identificadas"": [
      {
        ""area"": ""Técnica/Económica/Administrativa/Experiencia"",
        ""descripcion"": ""Descripción de la brecha"",
        ""diferencia_puntaje"": 0.0,
        ""diferencia_monto"": 0.0,
        ""impacto"": ""Alto/Medio/Bajo"",
        ""se_puede_mitigar"": true,
        ""recomendacion_mejora"": ""Acción concreta para reducir esta brecha en futuras licitaciones""
      }
    ]
  },
  ""documentos_adjuntos"": [
    {
      ""nombre"": ""Nombre del archivo adjunto listado en el acta"",
      ""tipo"": ""Resolución/Acta/Informe/Declaración/CDP/Anexo/Otros"",
      ""descripcion"": ""Breve descripción del contenido"",
      ""tamanio_kb"": 0
    }
  ],
  ""validacion_documental"": {
    ""documentos"": [
      {
        ""nombre"": ""Nombre del documento/antecedente"",
        ""requerido"": true,
        ""enviado"": true,
        ""observado_en_acta"": ""Qué dice el acta sobre este documento: faltante/observado/conforme (null si no lo menciona)"",
        ""estado"": ""ok | faltante | inconsistente | sin_informacion""
      }
    ],
    ""inconsistencias"": [
      {
        ""documento"": ""Nombre del documento afectado"",
        ""dice_acta"": ""Afirmación textual o parafraseada del acta (ej: 'no presentó garantía de seriedad')"",
        ""evidencia"": ""Qué evidencian los documentos adjuntos entregados (ej: 'la garantía figura entre los archivos presentados')"",
        ""severidad"": ""alta | media | baja""
      }
    ],
    ""resumen"": ""Veredicto de coherencia en 1-2 frases"",
    ""coherente"": true
  },
  ""metricas_clave"": {
    ""diferencia_puntaje_total"": 0.0,
    ""diferencia_monto_ofertado"": 0.0,
    ""diferencia_porcentaje_cumplimiento"": 0.0,
    ""cantidad_ofertantes"": 0,
    ""ranking_tivit"": 0,
    ""margen_mejora_tecnico"": 0.0,
    ""margen_mejora_economico"": 0.0
  },
  ""dashboard_kpis"": [
    { ""indicador"": ""Puntaje TIVIT vs Máximo"", ""valor"": ""XX%"", ""tendencia"": ""estable"", ""color"": ""yellow"" },
    { ""indicador"": ""Brecha vs Ganador"", ""valor"": ""-X puntos"", ""tendencia"": ""negativa"", ""color"": ""red"" },
    { ""indicador"": ""Diferencia Monto"", ""valor"": ""$X"", ""tendencia"": ""mejorable"", ""color"": ""yellow"" },
    { ""indicador"": ""Ranking"", ""valor"": ""#X de N"", ""tendencia"": ""mejorable"", ""color"": ""yellow"" },
    { ""indicador"": ""Cumplimiento Técnico"", ""valor"": ""XX%"", ""tendencia"": ""estable"", ""color"": ""green"" }
  ],
  ""recomendaciones_estrategicas"": [
    ""Recomendación accionable y específica para mejorar en licitaciones similares futuras""
  ],
  ""riesgos_identificados"": [
    { ""riesgo"": ""Descripción del riesgo"", ""nivel"": ""Alto/Medio/Bajo"", ""mitigacion"": ""Cómo mitigarlo"", ""impacto_estimado"": 0.0 }
  ]
}

VALIDACIÓN DOCUMENTAL (sección validacion_documental — crítica):
- Contrasta los documentos adjuntos entregados (documentos_adjuntos) contra los antecedentes requeridos (requisitos.antecedentes_requeridos) y contra lo que el acta declara como faltante u observado.
- Si el acta afirma que un documento faltó o fue observado, PERO ese documento aparece entre los adjuntos entregados, decláralo como INCONSISTENCIA (estado ""inconsistente"", severidad ""alta"") — NO repitas el motivo del acta sin verificarlo contra la evidencia.
- Si no hay información suficiente para saber qué se envió, usa estado ""sin_informacion"" — nunca asumas que no se envió nada.
- El motivo de pérdida (analisis_tivit) debe ser coherente con esta validación: si el motivo declarado se basa en un documento supuestamente faltante que sí fue entregado, indícalo explícitamente en la inconsistencia y en el resumen.

PROFUNDIDAD DEL ANÁLISIS (obligatorio):
- Cada motivo del resultado debe citar la evidencia del acta que lo sustenta (sección, criterio o texto de referencia).
- Las brechas de puntaje deben estar cuantificadas por criterio (diferencia exacta de puntos y su ponderación).
- Las recomendaciones estratégicas deben ser accionables y estar priorizadas por impacto (la primera es la de mayor impacto).
- Las fortalezas y debilidades deben referenciar el criterio de evaluación concreto que las origina, no ser genéricas.

IMPORTANTE: El objetivo NO es solo describir por qué TIVIT perdió. El objetivo es EXTRAER todos los datos técnicos, financieros y de evaluación disponibles para que un analista humano pueda:
1. Entender COMPLETAMENTE la licitación y su proceso
2. Identificar patrones en las evaluaciones
3. Tomar decisiones informadas para futuras ofertas
4. Tener métricas comparables entre distintas licitaciones";
    }
}

public class GeminiResponse
{
    public string Text { get; set; } = string.Empty;
    public GeminiUsage Usage { get; set; } = new();
    public string RawResponse { get; set; } = string.Empty;
}

public class GeminiUsage
{
    public int PromptTokenCount { get; set; }
    public int CandidatesTokenCount { get; set; }
    public int TotalTokenCount { get; set; }
}

public class GeminiChatResponse
{
    public string Text { get; set; } = string.Empty;
    public string FinishReason { get; set; } = string.Empty;
}

public class ChatHistoryItem
{
    public string Rol { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
}
