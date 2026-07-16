using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Interpreta una consulta de búsqueda en lenguaje natural vía Gemini en Vertex AI: expande
/// sinónimos/conceptos del dominio e infiere filtros implícitos (estado, monto, fecha).
/// 018-buscador-inteligente-nl, research.md — mismo patrón que
/// <c>MPM.Modules.Alertas.Services.SinonimosIaService</c>, replicado localmente (Principio I:
/// cada módulo construye su propia llamada a Gemini, no se comparte cliente entre módulos).
///
/// Modelo: gemini-2.5-flash-lite (USD 0.10/0.40 por millón de tokens entrada/salida) — el más
/// barato que cubre esta categoría de extracción; ver research.md para el plan de escalón a
/// gemini-3.1-flash-lite si el recall de sinónimos no alcanza SC-002.
/// </summary>
public class ConsultaSemanticaService(
    HttpClient httpClient, IConfiguration config, GoogleAdcTokenProvider tokenProvider, ILogger<ConsultaSemanticaService> logger)
{
    private const string ModelName = "gemini-2.5-flash-lite";

    // virtual para poder mockearlo en LicitacionServiceTests (mismo patrón que
    // GoogleAdcTokenProvider.GetAccessTokenAsync)
    public virtual async Task<ConsultaSemanticaResult?> InterpretarAsync(string query, CancellationToken ct = default)
    {
        var projectId = config["GOOGLE_CLOUD_PROJECT"] ?? config["GoogleCloudProject"];
        if (string.IsNullOrWhiteSpace(projectId))
        {
            logger.LogWarning("GOOGLE_CLOUD_PROJECT no configurado — la búsqueda usa el texto literal sin interpretar");
            return null;
        }
        var region = config["Vertex:Region"] ?? "us-central1";

        var prompt = $"Analiza esta consulta en lenguaje natural sobre licitaciones públicas chilenas: '{query}'\n\n" + """
            Extrae:
            1. Entre 3 y 8 sinónimos o conceptos relacionados del dominio de licitaciones públicas
               (p. ej. "ciberseguridad" -> "SOC", "seguridad de la información", "protección de datos").
            2. Si la consulta menciona un estado de licitación, mapéalo a uno de estos códigos exactos:
               5=Publicada (activa/vigente), 6=Cerrada, 7=Desierta, 8=Adjudicada, 15=Revocada.
               Si no menciona ningún estado, deja estadoInferido en null.
            3. Si la consulta menciona un monto o rango de monto (p. ej. "mayores a 10 millones"),
               extrae montoDesde/montoHasta en pesos chilenos (10 millones = 10000000). Si no
               menciona monto, deja ambos en null.
            4. Si la consulta menciona un plazo relativo (p. ej. "el último mes"), extrae
               fechaDesde/fechaHasta en formato ISO 8601 (YYYY-MM-DD), usando hoy como referencia.
               Si no menciona plazo, deja ambos en null.
            5. confianza: "alta" si pudiste interpretar algo útil de la consulta, "baja" si la
               consulta es ambigua, vacía de contenido reconocible, o texto aleatorio.

            Responde solo JSON con esta forma exacta:
            {"terminosExpandidos": ["...", "..."], "estadoInferido": null, "montoDesde": null,
             "montoHasta": null, "fechaDesde": null, "fechaHasta": null, "confianza": "alta"}
            """;

        var request = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 1024, responseMimeType = "application/json" }
        };

        try
        {
            var token = await tokenProvider.GetAccessTokenAsync(ct);
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://{region}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{region}/publishers/google/models/{ModelName}:generateContent")
            { Content = content };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.SendAsync(httpRequest, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Vertex AI respondió {Status} al interpretar consulta '{Query}': {Body}", response.StatusCode, query, errorBody);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            return ParseRespuesta(body);
        }
        catch (Exception ex)
        {
            // Un fallo al interpretar la consulta no debe impedir la búsqueda -- se degrada a
            // búsqueda literal (FR-005), igual que SinonimosIaService con las reglas de Alertas.
            logger.LogWarning(ex, "Fallo interpretando consulta '{Query}'", query);
            return null;
        }
    }

    private ConsultaSemanticaResult? ParseRespuesta(string geminiResponseBody)
    {
        string? text;
        try
        {
            using var doc = JsonDocument.Parse(geminiResponseBody);
            text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Respuesta de Vertex AI con forma inesperada");
            return null;
        }

        if (string.IsNullOrWhiteSpace(text)) return null;

        // Gemini a veces envuelve el JSON en fences ```json``` pese a responseMimeType=application/json
        // (mismo comportamiento visto en GeminiService.ParseGeminiResponse de MPM.Modules.Analisis).
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                text = text[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            var raw = JsonSerializer.Deserialize<RawInterpretacion>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (raw == null) return null;

            return new ConsultaSemanticaResult
            {
                TerminosExpandidos = raw.TerminosExpandidos ?? new List<string>(),
                EstadoInferido = raw.EstadoInferido,
                MontoDesde = raw.MontoDesde,
                MontoHasta = raw.MontoHasta,
                FechaDesde = DateTime.TryParse(raw.FechaDesde, out var fd) ? fd : null,
                FechaHasta = DateTime.TryParse(raw.FechaHasta, out var fh) ? fh : null,
                Confianza = string.Equals(raw.Confianza, "alta", StringComparison.OrdinalIgnoreCase)
                    ? ConfianzaInterpretacion.Alta
                    : ConfianzaInterpretacion.Baja,
            };
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "No se pudo parsear la interpretación de Gemini: {Text}", text);
            return null;
        }
    }

    private class RawInterpretacion
    {
        public List<string>? TerminosExpandidos { get; set; }
        public short? EstadoInferido { get; set; }
        public decimal? MontoDesde { get; set; }
        public decimal? MontoHasta { get; set; }
        public string? FechaDesde { get; set; }
        public string? FechaHasta { get; set; }
        public string? Confianza { get; set; }
    }
}
