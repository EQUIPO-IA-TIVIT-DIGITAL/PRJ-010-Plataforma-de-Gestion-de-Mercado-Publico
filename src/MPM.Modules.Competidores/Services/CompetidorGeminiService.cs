using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Shared.Services;

namespace MPM.Modules.Competidores.Services;

/// <summary>
/// Cliente de Gemini vía Vertex AI para el análisis de patrones de un competidor -- mismo
/// mecanismo de autenticación (ADC vía GoogleAdcTokenProvider, ya en MPM.Shared) que usa
/// MPM.Modules.Analisis, pero con prompt de solo texto (sin PDF) sobre el listado de ofertas
/// de ese competidor. NUNCA se llama automáticamente -- solo cuando el usuario confirma
/// explícitamente un análisis para un competidor+rango específico (spec FR-004).
/// </summary>
public class CompetidorGeminiService(HttpClient httpClient, IConfiguration config, GoogleAdcTokenProvider tokenProvider, ILogger<CompetidorGeminiService> logger)
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
            generationConfig = new { temperature = 0.2, maxOutputTokens = 8192, responseMimeType = "application/json" }
        };

        var token = await tokenProvider.GetAccessTokenAsync(ct);
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, EndpointFor(ModelName))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        logger.LogInformation("Analizando competidor {Competidor} con Gemini (Vertex AI)", nombreCompetidor);
        var response = await httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini respondió {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
            ?? throw new InvalidOperationException("Respuesta de Gemini sin contenido de texto");

        return text;
    }
}
