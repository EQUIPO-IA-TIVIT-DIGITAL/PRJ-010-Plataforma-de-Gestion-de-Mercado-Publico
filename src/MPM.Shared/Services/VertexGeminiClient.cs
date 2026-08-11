using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MPM.Shared.Services;

/// <summary>
/// Cliente compartido de Gemini vía Vertex AI (autenticado con ADC, ver
/// <see cref="GoogleAdcTokenProvider"/>), usado por MPM.Modules.Analisis y
/// MPM.Modules.Competidores. Antes de 029-fix-hallazgos-code-review-competidores-alertas cada
/// módulo tenía su propia copia de este armado/parseo de request -- lo que dejó a Competidores
/// con un <c>maxOutputTokens</c> desincronizado del valor que Análisis ya había subido a 65536
/// para corregir un bug de truncamiento real. Centralizar acá evita que ese tipo de fix quede
/// aplicado en un solo lugar.
/// </summary>
public class VertexGeminiClient(HttpClient httpClient, IConfiguration config, GoogleAdcTokenProvider tokenProvider, ILogger<VertexGeminiClient> logger) : ILlmClient, IConfigurableLlmClient
{
    /// <summary>
    /// Límite de tokens de salida validado en producción para respuestas JSON estructuradas de
    /// Gemini (fue subido desde un valor menor tras un bug de truncamiento documentado). Los
    /// callers deben usar esta constante en su <c>generationConfig.maxOutputTokens</c> en vez de
    /// hardcodear su propio número.
    /// </summary>
    public const int DefaultMaxOutputTokens = 65536;

    /// <summary>Modelo por defecto del camino Gemini (puede ser sobreescrito con AI:Model).</summary>
    public const string DefaultModelName = "gemini-2.5-pro";

    private string? _modelOverride; // desde ApplySettings (config persistida del switch)

    /// <summary>Modelo activo: override del switch (BD) > env AI:Model > default Gemini.</summary>
    public string ModelName => _modelOverride ?? config["AI:Model"] ?? DefaultModelName;

    /// <summary>Recibe el modelo resuelto por request (config persistida del super admin).</summary>
    public void ApplySettings(string? endpoint, string model)
    {
        if (!string.IsNullOrWhiteSpace(model))
            _modelOverride = model;
    }

    /// <summary>
    /// Implementación de <see cref="ILlmClient"/>: traduce un <see cref="LlmRequest"/> neutral al
    /// formato nativo de Gemini (contents[]/parts[], fileData o inlineData para PDF, y
    /// responseMimeType=application/json si <c>JsonResponse</c>) y delega el envío/parseo en
    /// <see cref="GenerarContenidoAsync(string, object, CancellationToken)"/>.
    /// </summary>
    public async Task<LlmResult> GenerarContenidoAsync(LlmRequest request, CancellationToken ct = default)
    {
        var result = await GenerarContenidoAsync(ModelName, BuildRequestBody(request), ct);
        return new LlmResult(
            result.Text,
            result.RawResponse,
            new LlmUsage(result.Usage.PromptTokenCount, result.Usage.CandidatesTokenCount, result.Usage.TotalTokenCount),
            result.FinishReason);
    }

    private static object BuildRequestBody(LlmRequest request)
    {
        var body = new Dictionary<string, object?>
        {
            ["contents"] = request.Messages.Select(m => new
            {
                role = m.Role == "assistant" ? "model" : m.Role,
                parts = m.Parts.Select(ToGeminiPart).ToArray()
            }).ToArray()
        };

        if (!string.IsNullOrWhiteSpace(request.SystemInstruction))
            body["systemInstruction"] = new { parts = new[] { new { text = request.SystemInstruction } } };

        var generationConfig = new Dictionary<string, object?>
        {
            ["temperature"] = request.Temperature,
            ["maxOutputTokens"] = request.MaxOutputTokens
        };
        if (request.JsonResponse)
            generationConfig["responseMimeType"] = "application/json";
        body["generationConfig"] = generationConfig;

        return body;
    }

    private static object ToGeminiPart(LlmPart part) => part switch
    {
        LlmTextPart t => new { text = t.Text },
        LlmPdfPart p when !string.IsNullOrWhiteSpace(p.GcsUri) =>
            new { fileData = new { mimeType = "application/pdf", fileUri = p.GcsUri } },
        LlmPdfPart p =>
            new { inlineData = new { mimeType = "application/pdf", data = Convert.ToBase64String(p.PdfBytes) } },
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, "Tipo de LlmPart no soportado por Gemini")
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string ProjectId => config["GOOGLE_CLOUD_PROJECT"]
        ?? throw new InvalidOperationException("GOOGLE_CLOUD_PROJECT no configurado");
    private string Region => config["Vertex:Region"] ?? "us-central1";

    private string EndpointFor(string model) =>
        $"https://{Region}-aiplatform.googleapis.com/v1/projects/{ProjectId}/locations/{Region}/publishers/google/models/{model}:generateContent";

    /// <summary>
    /// Arma y envía la solicitud a Vertex AI, y parsea la respuesta. <paramref name="requestBody"/>
    /// es el objeto completo (<c>contents</c>, <c>generationConfig</c>, opcionalmente
    /// <c>systemInstruction</c>) -- cada caller sigue controlando su propio prompt/config, este
    /// cliente solo centraliza auth, envío, manejo de errores y parseo de la respuesta.
    /// </summary>
    /// <exception cref="GeminiRespuestaBloqueadaException">
    /// Cuando Vertex AI responde sin <c>candidates</c> (ej. contenido bloqueado por el filtro de
    /// seguridad) -- antes de esta unificación, Análisis silenciaba este caso devolviendo texto
    /// vacío y Competidores lo dejaba propagarse como excepción no controlada; ahora ambos
    /// reciben el mismo error tipado y deciden cómo manejarlo.
    /// </exception>
    public async Task<VertexGeminiResult> GenerarContenidoAsync(string model, object requestBody, CancellationToken ct = default)
    {
        var token = await tokenProvider.GetAccessTokenAsync(ct);
        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, EndpointFor(model))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Gemini (Vertex AI) error {Status}: {Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        return ParseResponse(body);
    }

    private static VertexGeminiResult ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            throw new GeminiRespuestaBloqueadaException(
                "Gemini no devolvió candidates -- probablemente el contenido fue bloqueado por el filtro de seguridad.", json);
        }

        var first = candidates[0];
        var text = "";
        if (first.TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) &&
            parts.GetArrayLength() > 0)
        {
            text = parts[0].GetProperty("text").GetString() ?? "";
        }

        var finishReason = first.TryGetProperty("finishReason", out var fr) ? fr.GetString() ?? "" : "";

        // Strip markdown code fences si Gemini envuelve el JSON en ```json ... ```
        if (text.StartsWith("```"))
        {
            var newline = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```");
            if (newline >= 0 && lastFence > newline)
                text = text[(newline + 1)..lastFence].Trim();
        }
        // Asegura que el texto arranque en el primer '{' o '[' (quita cualquier preámbulo)
        var jsonStart = text.IndexOfAny(['{', '[']);
        if (jsonStart > 0)
            text = text[jsonStart..];

        var usage = new VertexGeminiUsage();
        if (root.TryGetProperty("usageMetadata", out var usageMeta))
        {
            usage.PromptTokenCount = usageMeta.TryGetProperty("promptTokenCount", out var ptc) ? ptc.GetInt32() : 0;
            usage.CandidatesTokenCount = usageMeta.TryGetProperty("candidatesTokenCount", out var ctc) ? ctc.GetInt32() : 0;
            usage.TotalTokenCount = usageMeta.TryGetProperty("totalTokenCount", out var ttc) ? ttc.GetInt32() : 0;
        }

        return new VertexGeminiResult
        {
            Text = text,
            FinishReason = finishReason,
            Usage = usage,
            RawResponse = json
        };
    }
}

public class VertexGeminiResult
{
    public string Text { get; set; } = string.Empty;
    public string FinishReason { get; set; } = string.Empty;
    public VertexGeminiUsage Usage { get; set; } = new();
    public string RawResponse { get; set; } = string.Empty;
}

public class VertexGeminiUsage
{
    public int PromptTokenCount { get; set; }
    public int CandidatesTokenCount { get; set; }
    public int TotalTokenCount { get; set; }
}

/// <summary>
/// Gemini respondió sin <c>candidates</c> -- típicamente el contenido fue bloqueado por el
/// filtro de seguridad de Vertex AI. Es un caso esperable y recuperable (el usuario puede
/// reintentar o el contenido simplemente no es analizable), no un error interno del sistema.
/// </summary>
public class GeminiRespuestaBloqueadaException(string message, string rawResponse) : LlmRespuestaBloqueadaException(message, rawResponse);
