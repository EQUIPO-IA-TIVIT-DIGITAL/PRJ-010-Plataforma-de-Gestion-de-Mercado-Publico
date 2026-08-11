using Microsoft.Extensions.Logging;
using MPM.Core.SystemConfig;
using MPM.Shared.Services;

namespace MPM.Modules.Competidores.Services;

/// <summary>
/// Análisis de patrones de un competidor, agnóstico al proveedor de IA (033-migracion-qwen-g4):
/// resuelve el cliente activo por request vía <see cref="LlmClientResolver"/> (BD > env >
/// default) — el mismo código sirve Gemini y Qwen. Prompt de solo texto (sin PDF) sobre el
/// listado de ofertas de ese competidor. NUNCA se llama automáticamente -- solo cuando el
/// usuario confirma explícitamente un análisis para un competidor+rango específico (spec FR-004).
///
/// Armado de request, auth y parseo de respuesta viven en el cliente <see cref="ILlmClient"/>
/// (MPM.Shared, ver VertexGeminiClient).
/// </summary>
public class CompetidorGeminiService(LlmClientResolver resolver, ILogger<CompetidorGeminiService> logger)
{
    public async Task<string> AnalizarCompetidorAsync(string nombreCompetidor, string ofertasResumen, CancellationToken ct = default)
    {
        var prompt = $$"""
            Eres un analista de inteligencia competitiva para licitaciones públicas chilenas.
            Analiza el siguiente listado de ofertas presentadas por el competidor "{{nombreCompetidor}}"
            y devuelve SOLO un JSON (sin markdown, sin texto adicional) con esta forma exacta:
            {
              "patrones": "texto describiendo qué tipo de licitaciones persigue este competidor",
              "organismosFrecuentes": ["lista de organismos donde más participa"],
              "montoPromedioOfertado": <número, monto promedio de sus ofertas>,
              "tasaExito": "<porcentaje o descripción de cuántas ofertas fueron aceptadas>",
              "recomendaciones": ["lista de recomendaciones concretas para competirle mejor"]
            }

            Listado de ofertas:
            {{ofertasResumen}}
            """;

        var request = new LlmRequest(
            Messages: [new LlmMessage("user", [new LlmTextPart(prompt)])],
            Temperature: 0.2,
            MaxOutputTokens: VertexGeminiClient.DefaultMaxOutputTokens,
            JsonResponse: true);

        var client = await resolver.GetClientAsync(ct);
        logger.LogInformation("Analizando competidor {Competidor} con {Model}", nombreCompetidor, client.ModelName);
        var result = await client.GenerarContenidoAsync(request, ct);

        return result.Text;
    }
}
