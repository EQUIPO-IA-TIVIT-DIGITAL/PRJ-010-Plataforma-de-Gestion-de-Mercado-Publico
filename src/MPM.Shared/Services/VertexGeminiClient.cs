using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MPM.Shared.Services;

/// <summary>
/// Cliente compartido de Gemini vía Vertex AI (autenticado con ADC, ver
/// <see cref="GoogleAdcTokenProvider"/>), usado por MPM.Modules.Analisis y
/// MPM.Modules.Competidores. 037-C: instrumentado con trazabilidad OTel (Activity llm.call),
/// métricas mpm_llm_* y persistencia llm_usage via LlmUsageService (nunca rompe flujo LLM).
/// </summary>
public class VertexGeminiClient(
    HttpClient httpClient,
    IConfiguration config,
    GoogleAdcTokenProvider tokenProvider,
    ILogger<VertexGeminiClient> logger,
    LlmUsageService? llmUsageService = null) : ILlmClient, IConfigurableLlmClient
{
    public const int DefaultMaxOutputTokens = 65536;
    public const string DefaultModelName = "gemini-2.5-pro";

    private string? _modelOverride;

    public string ModelName => _modelOverride ?? config["AI:Model"] ?? DefaultModelName;

    public void ApplySettings(string? endpoint, string model)
    {
        if (!string.IsNullOrWhiteSpace(model))
            _modelOverride = model;
    }

    public async Task<LlmResult> GenerarContenidoAsync(LlmRequest request, CancellationToken ct = default)
    {
        // 037-C: start Activity llm.call con tags provider/modelo (sin PII) y medir latencia
        var sw = Stopwatch.StartNew();
        var activity = StartLlmActivity("gemini", ModelName);
        VertexGeminiResult? innerResult = null;
        try
        {
            innerResult = await GenerarContenidoAsync(ModelName, BuildRequestBody(request), ct);
            var result = new LlmResult(
                innerResult.Text,
                innerResult.RawResponse,
                new LlmUsage(innerResult.Usage.PromptTokenCount, innerResult.Usage.CandidatesTokenCount, innerResult.Usage.TotalTokenCount),
                innerResult.FinishReason);
            activity?.SetTag("llm.prompt_tokens", innerResult.Usage.PromptTokenCount);
            activity?.SetTag("llm.completion_tokens", innerResult.Usage.CandidatesTokenCount);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection { ["exception.message"] = ex.Message }));
            throw;
        }
        finally
        {
            sw.Stop();
            activity?.SetTag("llm.latency_ms", sw.ElapsedMilliseconds);
            activity?.Dispose();
            // Persistir llm_usage + métricas (OBS-R008: nunca debe romper flujo LLM)
            if (innerResult != null)
            {
                try
                {
                    var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
                    if (llmUsageService != null)
                    {
                        // fire-and-forget sin bloquear (await but swallow exceptions inside service)
                        await llmUsageService.RegistrarAsync(
                            traceId, "gemini", ModelName,
                            innerResult.Usage.PromptTokenCount, innerResult.Usage.CandidatesTokenCount,
                            (int)sw.ElapsedMilliseconds, null, null, ct);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "VertexGeminiClient: fallo al registrar llm_usage (no crítico) modelo {Modelo}", ModelName);
                }
            }
            else
            {
                // Si no hay innerResult (excepción antes de usage), aún registrar latencia con 0 tokens
                try
                {
                    if (llmUsageService != null)
                    {
                        var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
                        await llmUsageService.RegistrarAsync(traceId, "gemini", ModelName, 0, 0, (int)sw.ElapsedMilliseconds, null, null, ct);
                    }
                }
                catch (Exception ex) { logger.LogDebug(ex, "VertexGeminiClient fallback llm_usage sin innerResult modelo {Modelo}", ModelName); }
            }
        }
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

        if (text.StartsWith("```"))
        {
            var newline = text.IndexOf('\n');
            var lastFence = text.LastIndexOf("```");
            if (newline >= 0 && lastFence > newline)
                text = text[(newline + 1)..lastFence].Trim();
        }
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

    private Activity? StartLlmActivity(string provider, string modelo)
    {
        try
        {
            // Intentar reusar MpmActivitySource.Instance via reflection (sin dependencia compile-time a MPM.Core)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("MPM.Core.Observability.MpmActivitySource");
                if (t != null)
                {
                    var instProp = t.GetProperty("Instance");
                    var src = instProp?.GetValue(null) as ActivitySource;
                    if (src != null)
                    {
                        var act = src.StartActivity("llm.call", ActivityKind.Internal);
                        act?.SetTag("llm.provider", provider);
                        act?.SetTag("llm.modelo", modelo);
                        act?.SetTag("llm.system", provider);
                        return act;
                    }
                }
            }
            // Fallback: source local con mismo nombre
            var fallback = new ActivitySource("MPM.Api", "1.0.0");
            var a = fallback.StartActivity("llm.call", ActivityKind.Internal);
            a?.SetTag("llm.provider", provider);
            a?.SetTag("llm.modelo", modelo);
            return a;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "StartLlmActivity fallback");
            return null;
        }
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

public class GeminiRespuestaBloqueadaException(string message, string rawResponse) : LlmRespuestaBloqueadaException(message, rawResponse);
