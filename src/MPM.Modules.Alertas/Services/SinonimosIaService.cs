using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MPM.Shared.Services;

namespace MPM.Modules.Alertas.Services;

/// <summary>
/// Expande una keyword a sinónimos/conceptos relacionados vía Gemini en Vertex AI, calculado
/// una sola vez al crear/editar la regla (research.md §2 de 003-fase6-alertas-keywords), no en
/// cada ciclo de matching.
///
/// Autenticación vía ADC (020-migracion-gemini-adc), no API key — ver
/// <see cref="GoogleAdcTokenProvider"/>. Mismo patrón HttpClient directo (sin SDK de Vertex)
/// que <c>MPM.Modules.Analisis.Services.GeminiService</c>; este módulo no referencia el
/// proyecto de Analisis (Principio I), cada uno construye su propia llamada.
/// </summary>
public class SinonimosIaService(
    HttpClient httpClient, IConfiguration config, GoogleAdcTokenProvider tokenProvider, ILogger<SinonimosIaService> logger)
{
    private const string ModelName = "gemini-2.5-flash";

    public async Task<List<string>?> ExpandirAsync(string keyword, CancellationToken ct = default)
    {
        var projectId = config["GOOGLE_CLOUD_PROJECT"] ?? config["GoogleCloudProject"];
        if (string.IsNullOrWhiteSpace(projectId))
        {
            logger.LogWarning("GOOGLE_CLOUD_PROJECT no configurado — la regla se guarda sin sinónimos");
            return null;
        }
        var region = config["Vertex:Region"] ?? "us-central1";

        var prompt = $"Dado el término de búsqueda de licitaciones públicas '{keyword}', " +
            "devuelve entre 5 y 10 sinónimos o términos relacionados que un comprador público " +
            "podría usar en el nombre o descripción de una licitación. " +
            "Responde solo JSON con esta forma exacta: {\"sinonimos\": [\"...\", \"...\"]}";

        var request = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.3, maxOutputTokens = 1024, responseMimeType = "application/json" }
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
                logger.LogWarning("Vertex AI respondió {Status} al expandir sinónimos de '{Keyword}': {Body}", response.StatusCode, keyword, errorBody);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var text = ExtraerTexto(body);
            if (text == null) return null;

            var parsed = JsonSerializer.Deserialize<SinonimosResponse>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return parsed?.Sinonimos;
        }
        catch (Exception ex)
        {
            // Un fallo al expandir sinónimos no debe impedir crear la regla — se guarda con
            // sinonimos_ia=null y se puede reintentar editando la regla.
            logger.LogWarning(ex, "Fallo expandiendo sinónimos para '{Keyword}'", keyword);
            return null;
        }
    }

    private static string? ExtraerTexto(string geminiResponseBody)
    {
        using var doc = JsonDocument.Parse(geminiResponseBody);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
    }

    private class SinonimosResponse
    {
        public List<string>? Sinonimos { get; set; }
    }
}
