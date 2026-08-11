using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPM.Core.SystemConfig;
using MPM.Modules.Licitaciones.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Interpreta una consulta de búsqueda en lenguaje natural vía el proveedor de IA activo
/// (033-migracion-qwen-g4): expande sinónimos/conceptos del dominio e infiere filtros
/// implícitos (estado, monto, fecha). Antes de esa spec llamaba directo a Gemini/Vertex AI
/// con HTTP crudo duplicado (018-buscador-inteligente-nl); ahora usa <see cref="LlmClientResolver"/>
/// como el resto de módulos. El contrato de salida JSON no cambia.
/// </summary>
public class ConsultaSemanticaService(
    LlmClientResolver resolver,
    ILogger<ConsultaSemanticaService> logger)
{
    // virtual para poder mockearlo en LicitacionServiceTests (mismo patrón que antes).
    public virtual async Task<ConsultaSemanticaResult?> InterpretarAsync(string query, CancellationToken ct = default)
    {
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

        try
        {
            var request = new LlmRequest(
                Messages: [new LlmMessage("user", [new LlmTextPart(prompt)])],
                Temperature: 0.2,
                MaxOutputTokens: 1024,
                JsonResponse: true);

            var client = await resolver.GetClientAsync(ct);
            var result = await client.GenerarContenidoAsync(request, ct);
            return ParseRespuesta(result.Text);
        }
        catch (Exception ex)
        {
            // Un fallo al interpretar la consulta no debe impedir la búsqueda -- se degrada a
            // búsqueda literal (FR-005), igual que SinonimosIaService con las reglas de Alertas.
            logger.LogWarning(ex, "Fallo interpretando consulta '{Query}'", query);
            return null;
        }
    }

    private ConsultaSemanticaResult? ParseRespuesta(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // El modelo a veces envuelve el JSON en fences ```json``` pese a pedir JSON mode
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
            logger.LogWarning(ex, "No se pudo parsear la interpretación del modelo: {Text}", text);
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
