using Microsoft.Extensions.Logging;
using MPM.Shared.Services;

namespace MPM.Modules.Competidores.Services;

/// <summary>
/// Cliente de Gemini vía Vertex AI para el análisis de patrones de un competidor -- mismo
/// mecanismo de autenticación (ADC vía GoogleAdcTokenProvider, ya en MPM.Shared) que usa
/// MPM.Modules.Analisis, pero con prompt de solo texto (sin PDF) sobre el listado de ofertas
/// de ese competidor. NUNCA se llama automáticamente -- solo cuando el usuario confirma
/// explícitamente un análisis para un competidor+rango específico (spec FR-004).
///
/// Armado de request, auth y parseo de respuesta viven en <see cref="VertexGeminiClient"/>
/// (MPM.Shared, compartido con MPM.Modules.Analisis desde
/// 029-fix-hallazgos-code-review-competidores-alertas) -- antes de esa spec este servicio
/// duplicaba esa lógica con su propio <c>maxOutputTokens</c> desincronizado (8192 vs. los 65536
/// que Análisis ya usaba tras corregir un bug de truncamiento real).
/// </summary>
public class CompetidorGeminiService(VertexGeminiClient vertexClient, ILogger<CompetidorGeminiService> logger)
{
    public const string ModelName = "gemini-2.5-pro";

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

        var request = new
        {
            contents = new[] { new { role = "user", parts = new object[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = VertexGeminiClient.DefaultMaxOutputTokens, responseMimeType = "application/json" }
        };

        logger.LogInformation("Analizando competidor {Competidor} con Gemini (Vertex AI)", nombreCompetidor);
        var result = await vertexClient.GenerarContenidoAsync(ModelName, request, ct);

        return result.Text;
    }
}
