using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MPM.Shared.Services;

/// <summary>
/// Cliente de proveedor de IA OpenAI-compatible (033-migracion-qwen-g4): sirve a Qwen 3.7 G4
/// servido por vLLM/Ollama/llama.cpp vía /v1/chat/completions. 037-C: instrumentado con
/// Activity llm.call, métricas mpm_llm_* y persistencia llm_usage (OBS-R008 nunca rompe flujo).
/// </summary>
public class OpenAiCompatClient(
    HttpClient httpClient,
    IConfiguration config,
    ILogger<OpenAiCompatClient> logger,
    LlmUsageService? llmUsageService = null) : ILlmClient, IConfigurableLlmClient
{
    private string? _endpoint;
    private string? _model;

    public string ModelName => _model ?? config["AI:Model"] ?? string.Empty;

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

        var sw = Stopwatch.StartNew();
        var activity = StartLlmActivity("openai", model);
        LlmResult? result = null;
        try
        {
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

            result = ParseResponse(responseBody);
            activity?.SetTag("llm.prompt_tokens", result.Usage.PromptTokenCount);
            activity?.SetTag("llm.completion_tokens", result.Usage.CandidatesTokenCount);
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
            try
            {
                var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
                var prompt = result?.Usage.PromptTokenCount;
                var completion = result?.Usage.CandidatesTokenCount;
                if (llmUsageService != null)
                {
                    await llmUsageService.RegistrarAsync(
                        traceId, "openai", model,
                        prompt.HasValue ? (int?)prompt.Value : 0,
                        completion.HasValue ? (int?)completion.Value : 0,
                        (int)sw.ElapsedMilliseconds, null, null, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OpenAiCompatClient: fallo al registrar llm_usage modelo {Modelo}", model);
            }
        }
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

    private static Activity? StartLlmActivity(string provider, string modelo)
    {
        try
        {
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
            var fallback = new ActivitySource("MPM.Api", "1.0.0");
            var a = fallback.StartActivity("llm.call", ActivityKind.Internal);
            a?.SetTag("llm.provider", provider);
            a?.SetTag("llm.modelo", modelo);
            return a;
        }
        catch
        {
            return null;
        }
    }
}
