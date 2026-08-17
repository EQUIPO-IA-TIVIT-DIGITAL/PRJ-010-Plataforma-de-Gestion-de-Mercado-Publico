using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MPM.Shared.Services;

/// <summary>
/// Cliente de proveedor de IA OpenAI-compatible (033-migracion-qwen-g4): sirve a Qwen 3.7 G4
/// servido por vLLM/Ollama/llama.cpp (o cualquier MaaS compatible) vía <c>/v1/chat/completions</c>.
/// Traduce el <see cref="LlmRequest"/> neutral a <c>messages[]</c> (texto + PDF como data URI),
/// usa <c>response_format: json_object</c> cuando <c>JsonResponse</c>, y parsea
/// <c>choices[0].message.content</c> + <c>finish_reason</c> + <c>usage</c>.
///
/// Endpoint y modelo se resuelven por request vía <see cref="ApplySettings"/> (los entrega
/// <c>LlmClientResolver</c> desde la configuración persistida o env) — el mismo patrón que
/// <see cref="VertexGeminiClient"/> con Gemini. Errores con la misma semántica tipada
/// (<see cref="LlmRespuestaBloqueadaException"/>).
/// </summary>
public class OpenAiCompatClient(
    HttpClient httpClient,
    IConfiguration config,
    ILogger<OpenAiCompatClient> logger) : ILlmClient, IConfigurableLlmClient
{
    private string? _endpoint;  // desde ApplySettings (BD) o env AI:Endpoint
    private string? _model;     // desde ApplySettings (BD) o env AI:Model

    public string ModelName => _model ?? config["AI:Model"] ?? string.Empty;

    /// <summary>Endpoint (base URL) y modelo del proveedor activo, resueltos por el resolver.</summary>
    public void ApplySettings(string? endpoint, string model)
    {
        _endpoint = endpoint;
        _model = model;
    }

    public async Task<LlmResult> GenerarContenidoAsync(LlmRequest request, CancellationToken ct = default)
    {
        var endpoint = _endpoint ?? config["AI:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("AI:Endpoint no configurado para el proveedor openai (Qwen).");
        var model = ModelName;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("AI:Model no configurado para el proveedor openai (Qwen).");

        var url = endpoint.TrimEnd('/') + "/chat/completions";

        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = request.Messages.Select(m => new
            {
                role = m.Role == "assistant" ? "assistant" : "user",
                content = m.Parts.Select(ToOpenAiPart).ToArray()
            }).ToArray(),
            ["temperature"] = request.Temperature,
            ["max_tokens"] = request.MaxOutputTokens
        };
        if (request.JsonResponse)
            body["response_format"] = new { type = "json_object" };

        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var apiKey = config["AI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var response = await httpClient.SendAsync(httpRequest, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Proveedor OpenAI-compatible error {Status}: {Body}", (int)response.StatusCode, responseBody);
            response.EnsureSuccessStatusCode();
        }

        return ParseResponse(responseBody);
    }

    private static object ToOpenAiPart(LlmPart part) => part switch
    {
        LlmTextPart t => new { type = "text", text = t.Text },
        LlmPdfPart p => new
        {
            type = "text",
            text = DocumentContentExtractor.FormatForPrompt(
                p.FileName,
                DocumentContentExtractor.ExtractTextFromPdf(p.PdfBytes))
        },
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Tipo de LlmPart no soportado por OpenAI-compatible")
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static LlmResult ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new LlmRespuestaBloqueadaException(
                "El proveedor no devolvió choices -- probablemente el contenido fue bloqueado o el body es anómalo.", json);

        var first = choices[0];
        var text = "";
        if (first.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.String)
        {
            text = content.GetString() ?? "";
        }

        var finishReason = first.TryGetProperty("finish_reason", out var fr) ? fr.GetString() ?? "" : "";

        var usage = new LlmUsage();
        if (root.TryGetProperty("usage", out var u))
        {
            usage = new LlmUsage(
                u.TryGetProperty("prompt_tokens", out var pt) && pt.ValueKind == JsonValueKind.Number ? pt.GetInt64() : 0,
                u.TryGetProperty("completion_tokens", out var ct) && ct.ValueKind == JsonValueKind.Number ? ct.GetInt64() : 0,
                u.TryGetProperty("total_tokens", out var tt) && tt.ValueKind == JsonValueKind.Number ? tt.GetInt64() : 0);
        }

        return new LlmResult(text, json, usage, finishReason);
    }
}
