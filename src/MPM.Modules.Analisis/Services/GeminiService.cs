using Microsoft.Extensions.Logging;
using MPM.Core.SystemConfig;
using MPM.Shared.Services;

namespace MPM.Modules.Analisis.Services;

/// <summary>
/// Servicio de análisis/chat de documentos de licitación, agnóstico al proveedor de IA
/// (033-migracion-qwen-g4). Antes de esa spec dependía directo de <see cref="VertexGeminiClient"/>
/// (Gemini/Vertex AI hardcodeado); ahora resuelve el cliente activo por request vía
/// <see cref="LlmClientResolver"/> (BD > env > default) — el mismo código sirve Gemini y Qwen.
///
/// Armado de request, auth y parseo de respuesta viven en el cliente <see cref="ILlmClient"/>
/// (MPM.Shared); este servicio solo construye el prompt/contenido específico de análisis de
/// PDFs y chat, en el formato neutral LlmRequest.
/// </summary>
public class GeminiService(LlmClientResolver resolver, ILogger<GeminiService> logger)
{
    /// <summary>Default histórico de Gemini (ya no es la fuente de verdad: el modelo activo se
    /// resuelve por request desde el cliente — <see cref="GetModelNameAsync"/>).</summary>
    public const string ModelName = VertexGeminiClient.DefaultModelName;

    /// <summary>Id del modelo que ejecutará el próximo análisis (se persiste en modelo_usado).</summary>
    public async Task<string> GetModelNameAsync(CancellationToken ct = default)
        => (await resolver.GetClientAsync(ct)).ModelName;

    /// <param name="gcsUri">
    /// Si el documento ya está en GCS (<c>gs://...</c>), se referencia directo sin mandar
    /// <paramref name="pdfBytes"/> en el body — más eficiente y evita el límite de tamaño de
    /// <c>inlineData</c>. Si es <c>null</c> (storage local), se usa <paramref name="pdfBytes"/>
    /// como <c>inlineData</c> en base64. (El formato gs:// solo lo soporta el camino Gemini;
    /// el camino Qwen envía siempre los bytes inline.)
    /// </param>
    public Task<GeminiResponse> AnalyzePdfAsync(byte[] pdfBytes, string fileName, string? gcsUri, CancellationToken ct = default) =>
        AnalyzeDocumentosAsync([(pdfBytes, fileName, gcsUri)], ct);

    /// <summary>
    /// 029-fix-hallazgos-code-review-competidores-alertas (FR-011/FR-012, QA BUG-005/BUG-010):
    /// envía todos los documentos de un workspace en una sola llamada (antes solo el primero).
    /// Se le da el contexto de todos a la vez para que pueda sintetizar información entre ellos
    /// Y detectar si alguno revoca/deja sin efecto a otro documento anterior del mismo
    /// workspace -- ambos bugs comparten la misma causa raíz (falta de contexto multi-documento).
    /// </summary>
    public async Task<GeminiResponse> AnalyzeDocumentosAsync(
        List<(byte[] Bytes, string FileName, string? GcsUri)> documentos, CancellationToken ct = default)
    {
        if (documentos.Count == 0)
            throw new ArgumentException("Debe proporcionarse al menos un documento", nameof(documentos));

        var prompt = GetAnalisisPrompt(documentos.Count);
        logger.LogInformation("Enviando {Count} documento(s) al proveedor de IA activo para análisis: {Files}",
            documentos.Count, string.Join(", ", documentos.Select(d => d.FileName)));

        var parts = new List<LlmPart>();
        foreach (var (bytes, fileName, gcsUri) in documentos)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".docx" || ext == ".doc" || ext == ".txt")
            {
                var text = DocumentContentExtractor.ExtractText(bytes, fileName);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(new LlmTextPart(DocumentContentExtractor.FormatForPrompt(fileName, text)));
                }
            }
            else
            {
                parts.Add(new LlmPdfPart(bytes, fileName, gcsUri));
            }
        }
        parts.Add(new LlmTextPart(prompt));

        var request = new LlmRequest(
            Messages: [new LlmMessage("user", parts)],
            Temperature: 0.2,
            MaxOutputTokens: VertexGeminiClient.DefaultMaxOutputTokens,
            JsonResponse: true);

        var client = await resolver.GetClientAsync(ct);
        logger.LogInformation("Respuesta de {Model} recibida", client.ModelName);

        var result = await client.GenerarContenidoAsync(request, ct);
        return new GeminiResponse
        {
            Text = result.Text,
            ModelName = client.ModelName,
            Usage = new GeminiUsage
            {
                PromptTokenCount = (int)(result.Usage.PromptTokenCount ?? 0),
                CandidatesTokenCount = (int)(result.Usage.CandidatesTokenCount ?? 0),
                TotalTokenCount = (int)(result.Usage.TotalTokenCount ?? 0)
            },
            RawResponse = result.RawResponse
        };
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

        var messages = new List<LlmMessage>();

        foreach (var h in historial.TakeLast(20))
        {
            var rolNeutral = h.Rol == "assistant" ? "assistant" : "user";
            messages.Add(new LlmMessage(rolNeutral, [new LlmTextPart(h.Contenido)]));
        }

        // Only add the current message if it's not already the last in history
        var lastHistorial = historial.LastOrDefault();
        if (lastHistorial == null || lastHistorial.Contenido != mensaje)
        {
            messages.Add(new LlmMessage("user", [new LlmTextPart(mensaje)]));
        }

        var request = new LlmRequest(
            Messages: messages,
            SystemInstruction: systemInstruction,
            Temperature: 0.3,
            MaxOutputTokens: 2048,
            JsonResponse: false);

        var client = await resolver.GetClientAsync(ct);
        var result = await client.GenerarContenidoAsync(request, ct);
        return new GeminiChatResponse
        {
            Text = result.Text,
            FinishReason = result.FinishReason
        };
    }

    /// <summary>
    /// Prompt de análisis de documentos (público para el harness de benchmark de
    /// tools/BenchmarkLlm, 033-migracion-qwen-g4 US2 — el benchmark debe usar EXACTAMENTE el
    /// mismo prompt que producción para que la comparación sea apples-to-apples).
    /// </summary>
    public static string GetAnalisisPrompt(int documentCount = 1)
    {
        var contextoDocumentos = documentCount > 1
            ? $"Se te están proporcionando {documentCount} documentos del MISMO workspace de análisis (se espera que sean de la misma licitación). Trátalos como un conjunto: sintetiza la información de TODOS ellos en UN ÚNICO objeto JSON de salida (no describas solo el primero, y NUNCA respondas con un array/lista de objetos -- tu respuesta completa debe ser el único objeto JSON descrito más abajo, sin importar cuántos documentos recibiste). Presta atención especial a si alguno de los documentos posteriores REVOCA, DEJA SIN EFECTO, o corrige formalmente una conclusión de otro documento anterior del mismo conjunto (ver sección \"revocacion\" del JSON) -- en ese caso, el resultado final vigente es el del documento que revoca, no el revocado. Si tras leerlos notas que en realidad describen licitaciones DISTINTAS (no deberían estar en el mismo workspace), usa como base la licitación del documento más reciente/completo para el JSON principal, y deja constancia de la discrepancia en `riesgos_identificados` -- igual debes devolver un único objeto, nunca un array."
            : "Se te está proporcionando un único documento de evaluación de licitación.";

        // NOTA: el bloque grande de abajo es un string verbatim NO interpolado (@"...") a
        // propósito -- contiene el JSON de ejemplo completo, lleno de llaves { } literales que
        // romperían la interpolación de C# ($"...") si se mezclaran. Por eso el contexto
        // multi-documento se concatena aparte en vez de insertarse inline.
        return contextoDocumentos + "\n\n" + @"Eres un analista experto en licitaciones públicas chilenas (Ley 19.886). Analiza TÉCNICAMENTE el/los documento(s) y extrae TODA la información relevante estructurada en el siguiente JSON. No omitas ningún campo disponible.

REGLAS GENERALES:
- Extrae SOLO información explícitamente presente en el/los documento(s)
- Si un dato no está disponible, usa null (nunca inventes)
- Textos siempre en español, formato claro y analítico
- Los montos deben ser numéricos (sin puntos ni separadores)
- Las fechas en formato YYYY-MM-DD
- Sé exhaustivo: cada criterio, subcriterio, ponderación y puntaje debe capturarse individualmente
- MONEDA (crítico): para cada monto, identifica la moneda REAL indicada explícitamente junto a esa cifra en el texto fuente (CLP/USD/UF/EUR). NUNCA asumas dólares (USD) por defecto -- si el texto no indica moneda explícita para una cifra, usa ""NO_DETERMINADA"" en el campo `_moneda` correspondiente, no adivines.
- ADMISIBILIDAD (crítico): marca a un oferente como ""Inadmisible"" ÚNICAMENTE cuando el documento lo declara explícitamente así con esas palabras o equivalentes (ej. ""se declara inadmisible"", ""queda fuera de bases""). NO confundas ""sin puntaje/monto visible en esta sección del documento"" con ""declarado inadmisible"" -- son cosas distintas; usa ""Desconocido"" cuando no haya declaración explícita.
- MONTO ESTIMADO vs. MONTO OFERTADO (crítico): `licitacion.monto_estimado` es el PRESUPUESTO que el organismo fijó ANTES de recibir ofertas (aparece típicamente en las bases o en la ficha de la licitación, antes de la apertura de ofertas) -- es un valor independiente de lo que cualquier participante ofertó. NUNCA copies ahí el monto ofertado por TIVIT ni por ningún competidor (`adjudicacion.ofertantes[].monto_ofertado`, `analisis_tivit.monto_ofertado`) aunque sean el primer monto relevante que encuentres en el texto.
- METRICAS_CLAVE (crítico): `metricas_clave.diferencia_puntaje_total` y `metricas_clave.diferencia_monto_ofertado` se calculan SIEMPRE como (TIVIT - ganador): un valor positivo significa que TIVIT obtuvo/ofertó más que el ganador, negativo que obtuvo/ofertó menos. No uses ninguna otra base de comparación (ej. contra el promedio de ofertantes o contra el segundo lugar) para estos dos campos.

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
    ""monto_estimado_moneda"": ""CLP/USD/UF/EUR/NO_DETERMINADA -- la moneda REAL indicada junto a esta cifra en el texto, nunca asumida"",
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
      ""monto_adjudicado_moneda"": ""CLP/USD/UF/EUR/NO_DETERMINADA -- moneda real de esta cifra, nunca asumida"",
      ""cantidad_ofertas_recibidas"": 0
    },
    ""ofertantes"": [
      {
        ""nombre"": ""Nombre del ofertante"",
        ""rut"": ""RUT del ofertante"",
        ""monto_ofertado"": 0.0,
        ""monto_ofertado_moneda"": ""CLP/USD/UF/EUR/NO_DETERMINADA -- moneda real de esta cifra, nunca asumida"",
        ""puntaje_total"": 0.0,
        ""resultado"": ""Adjudicado/No adjudicado/Inadmisible/Desistido -- SOLO 'Inadmisible' si el documento lo declara explícitamente así; si simplemente no hay puntaje/monto visible para este oferente en esta sección, usa 'Desconocido' en vez de asumir Inadmisible"",
        ""motivo_inadmisibilidad"": ""Si resultado='Inadmisible', el texto exacto (o parafraseado) que declara la inadmisibilidad; null si no aplica""
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
    ""monto_ofertado_moneda"": ""CLP/USD/UF/EUR/NO_DETERMINADA -- moneda real de esta cifra, nunca asumida"",
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
  ],
  ""revocacion"": {
    ""detectada"": false,
    ""documento_que_revoca"": ""Nombre/identificador del documento que declara la revocación (null si detectada=false)"",
    ""documento_revocado"": ""Nombre/identificador del documento cuya conclusión queda sin efecto (null si detectada=false)"",
    ""motivo"": ""Texto (o paráfrasis) de por qué se revoca (null si detectada=false)"",
    ""resultado_vigente"": ""El resultado/conclusión que debe considerarse vigente tras la revocación (null si detectada=false)""
  }
}

REVOCACIÓN ENTRE DOCUMENTOS (sección revocacion — crítica, solo aplica con más de un documento):
- Si y solo si uno de los documentos proporcionados declara EXPLÍCITAMENTE que otro documento anterior del mismo conjunto queda sin efecto, se revoca, se deja sin efecto, o se anula (ej. ""DÉJESE sin efecto la Resolución Exenta N°...""), completa esta sección con detectada=true y el detalle.
- NO infieras revocación de una simple discrepancia o contradicción entre documentos que no la declaren explícitamente -- eso no es revocación, es fuera de alcance de esta sección.
- Si detectada=true, el resto del análisis (analisis_tivit.resultado, adjudicacion, etc.) debe reflejar el resultado_vigente (posterior a la revocación), no la conclusión ya revocada.
- Con un solo documento, o sin revocación explícita detectada, deja detectada=false y el resto de los campos en null.

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
    /// <summary>Modelo que realmente ejecutó el análisis (se persiste en modelo_usado).</summary>
    public string ModelName { get; set; } = string.Empty;
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
