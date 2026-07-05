using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MPM.Modules.Analisis.Services;

public class GeminiService(HttpClient httpClient, ILogger<GeminiService> logger)
{
    public const string ModelName = "gemini-2.5-pro";
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<GeminiResponse> AnalyzePdfAsync(byte[] pdfBytes, string fileName, string apiKey, CancellationToken ct = default)
    {
        var prompt = GetAnalisisPrompt();
        logger.LogInformation("Enviando PDF {File} a Gemini para análisis ({Size} bytes)", fileName, pdfBytes.Length);

        // Try File API first (better support for scanned/image-based PDFs)
        try
        {
            return await AnalyzePdfViaFileApiAsync(pdfBytes, fileName, apiKey, prompt, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning("File API falló ({Msg}), reintentando con inline_data", ex.Message);
        }

        // Fallback: inline_data
        var pdfBase64 = Convert.ToBase64String(pdfBytes);
        var request = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { inline_data = new { mime_type = "application/pdf", data = pdfBase64 } },
                        new { text = prompt }
                    }
                }
            },
            generation_config = new { temperature = 0.2, max_output_tokens = 65536, response_mime_type = "application/json" }
        };

        var jsonRequest = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent?key={apiKey}",
            content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Gemini API error {Status}: {Body}", (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var jsonResponse = await response.Content.ReadAsStringAsync(ct);
        logger.LogInformation("Respuesta de Gemini recibida ({Length} chars)", jsonResponse.Length);
        return await ParseGeminiResponse(jsonResponse);
    }

    private async Task<GeminiResponse> AnalyzePdfViaFileApiAsync(byte[] pdfBytes, string fileName, string apiKey, string prompt, CancellationToken ct)
    {
        // Step 1: Upload to Gemini File API
        var uploadContent = new ByteArrayContent(pdfBytes);
        uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        uploadContent.Headers.Add("X-Goog-Upload-Protocol", "raw");
        uploadContent.Headers.Add("X-Goog-Upload-Header-Content-Type", "application/pdf");

        var uploadResponse = await httpClient.PostAsync(
            $"https://generativelanguage.googleapis.com/upload/v1beta/files?key={apiKey}",
            uploadContent, ct);

        if (!uploadResponse.IsSuccessStatusCode)
        {
            var err = await uploadResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"File upload failed {(int)uploadResponse.StatusCode}: {err[..Math.Min(200, err.Length)]}");
        }

        var uploadJson = await uploadResponse.Content.ReadAsStringAsync(ct);
        using var uploadDoc = JsonDocument.Parse(uploadJson);
        var fileElement = uploadDoc.RootElement.GetProperty("file");
        var fileUri = fileElement.GetProperty("uri").GetString()
            ?? throw new InvalidOperationException("No fileUri in upload response");
        var fileName2 = fileElement.GetProperty("name").GetString()
            ?? throw new InvalidOperationException("No file name in upload response");

        logger.LogInformation("PDF subido a Gemini File API: {Uri}", fileUri);

        // Gemini procesa el archivo de forma asíncrona (state: PROCESSING -> ACTIVE).
        // Si se llama a generateContent mientras sigue en PROCESSING, el modelo responde
        // como si no se hubiera enviado ningún documento. Hacemos polling hasta ACTIVE.
        await EsperarArchivoActivoAsync(fileName2, apiKey, ct);

        // Step 2: Analyze using file_data reference
        var request = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { file_data = new { mime_type = "application/pdf", file_uri = fileUri } },
                        new { text = prompt }
                    }
                }
            },
            generation_config = new { temperature = 0.2, max_output_tokens = 65536, response_mime_type = "application/json" }
        };

        var jsonRequest = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent?key={apiKey}",
            content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Gemini API error {Status}: {Body}", (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var jsonResponse = await response.Content.ReadAsStringAsync(ct);
        logger.LogInformation("Respuesta de Gemini (File API) recibida ({Length} chars)", jsonResponse.Length);
        return await ParseGeminiResponse(jsonResponse);
    }

    private async Task EsperarArchivoActivoAsync(string fileName, string apiKey, CancellationToken ct)
    {
        const int maxIntentos = 10;
        var delayMs = 1000;

        for (var intento = 1; intento <= maxIntentos; intento++)
        {
            var response = await httpClient.GetAsync(
                $"https://generativelanguage.googleapis.com/v1beta/{fileName}?key={apiKey}", ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var state = doc.RootElement.TryGetProperty("state", out var s) ? s.GetString() : null;

                if (state == "ACTIVE") return;
                if (state == "FAILED")
                    throw new InvalidOperationException($"Gemini File API reportó FAILED para {fileName}");

                logger.LogInformation("Archivo {File} en Gemini aún en estado {State}, esperando ({Intento}/{Max})",
                    fileName, state, intento, maxIntentos);
            }

            await Task.Delay(delayMs, ct);
            delayMs = Math.Min(delayMs * 2, 5000);
        }

        throw new InvalidOperationException($"Gemini File API no llegó a ACTIVE tras {maxIntentos} intentos para {fileName}");
    }

    public async Task<GeminiChatResponse> ChatAsync(string mensaje, string contextoAnalisis, List<ChatHistoryItem> historial, string apiKey, CancellationToken ct = default)
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
            system_instruction = new { parts = new[] { new { text = systemInstruction } } },
            contents,
            generation_config = new
            {
                temperature = 0.3,
                max_output_tokens = 2048
            }
        };

        var jsonRequest = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent?key={apiKey}",
            content, ct);

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
