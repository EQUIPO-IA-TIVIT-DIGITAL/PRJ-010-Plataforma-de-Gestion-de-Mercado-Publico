using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MPM.Shared.Services;

/// <summary>
/// 037-C: Servicio transversal que registra cada llamada LLM en `llm_usage` via
/// `usp_LlmUsage_Registrar` (calcula costo via `llm_model_pricing`) y actualiza
/// métricas `mpm_llm_*`. Nunca debe fallar la llamada LLM (OBS-R008): todo
/// insert está envuelto en try/catch y solo loguea Warning.
/// Provider/modelo sin PII (OBS-R005). Latencia en ms, costo en CLP.
/// </summary>
public class LlmUsageService(
    IConfiguration config,
    ILogger<LlmUsageService> logger)
{
    private string ConnectionString =>
        config.GetConnectionString("PostgreSQL")
        ?? config["ConnectionStrings:PostgreSQL"]
        ?? "Host=db;Port=5432;Database=mpm;Username=mpm;Password=mpm_password";

    /// <summary>
    /// Registra una llamada LLM. No lanza excepción nunca (OBS-R008).
    /// </summary>
    public async Task RegistrarAsync(
        string traceId,
        string provider,
        string modelo,
        int? promptTokens,
        int? completionTokens,
        int? latencyMs,
        long? licitacionId = null,
        long? workspaceId = null,
        CancellationToken ct = default)
    {
        // Normalizar provider/modelo (sin PII, solo valores controlados OBS-R003)
        provider = (provider ?? "unknown").Trim().ToLowerInvariant();
        if (provider is "vertex") provider = "gemini";
        if (provider is "qwen") provider = "openai"; // qwen via OpenAI-compatible -> label openai para consistencia con métrica
        // Mantener gemini/openai como únicos valores para métricas (OBS-R003)
        if (provider != "gemini" && provider != "openai")
            provider = provider is "openai" or "gemini" ? provider : "openai";

        modelo = (modelo ?? "unknown").Trim();
        if (modelo.Length > 50) modelo = modelo[..50];

        // TraceId W3C 32 hex chars; fallback a Activity.Current o random
        if (string.IsNullOrWhiteSpace(traceId) || traceId.Length < 8)
            traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        traceId = traceId.Trim().ToLowerInvariant();
        if (traceId.Length > 32) traceId = traceId[^32..];
        if (traceId.Length < 32)
            traceId = traceId.PadLeft(32, '0');

        // Feature-flag: Langfuse deshabilitado no bloquea (solo log debug)
        var langfuseEnabled = config.GetValue<bool>("Langfuse:Enabled") || config.GetValue<bool>("Langfuse__Enabled");
        if (langfuseEnabled)
            logger.LogDebug("LlmUsage Langfuse enabled - trace {TraceId} provider {Provider} modelo {Modelo}", traceId, provider, modelo);

        // Métricas (sin PII) - via reflection a MPM.Core.Observability.MpmMetrics para no crear dependencia circular
        try
        {
            IncrementMetricsReflection(provider, modelo, promptTokens, completionTokens, latencyMs);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Metrics increment via reflection failed (no crítico)");
        }

        // Persistir en BD - nunca debe throw (OBS-R008)
        try
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync(ct);
            // Dapper CALL con INOUT p_error_msg
            var parms = new DynamicParameters();
            parms.Add("p_trace_id", traceId);
            parms.Add("p_provider", provider);
            parms.Add("p_modelo", modelo);
            parms.Add("p_prompt_tokens", promptTokens);
            parms.Add("p_completion_tokens", completionTokens);
            parms.Add("p_latency_ms", latencyMs);
            parms.Add("p_licitacion_id", licitacionId);
            parms.Add("p_workspace_id", workspaceId);
            parms.Add("p_error_msg", "", direction: System.Data.ParameterDirection.InputOutput);

            await conn.ExecuteAsync("CALL usp_LlmUsage_Registrar(@p_trace_id, @p_provider, @p_modelo, @p_prompt_tokens, @p_completion_tokens, @p_latency_ms, @p_licitacion_id, @p_workspace_id, @p_error_msg)", parms, commandTimeout: 5);

            var errorMsg = parms.Get<string?>("p_error_msg");
            if (!string.IsNullOrWhiteSpace(errorMsg))
                logger.LogWarning("usp_LlmUsage_Registrar devolvió error {Error} trace {TraceId} provider {Provider}", errorMsg, traceId, provider);
            else
                logger.LogDebug("llm_usage registrado trace {TraceId} provider {Provider} modelo {Modelo} tokens {Prompt}/{Completion} latency {Latency}ms",
                    traceId, provider, modelo, promptTokens, completionTokens, latencyMs);
        }
        catch (Exception ex)
        {
            // Nunca propagar - solo Warning para no romper flujo LLM (OBS-R008)
            logger.LogWarning(ex, "No se pudo registrar llm_usage trace {TraceId} provider {Provider} modelo {Modelo} - el análisis continúa sin costo",
                traceId, provider, modelo);
        }
    }

    private void IncrementMetricsReflection(string provider, string modelo, int? promptTokens, int? completionTokens, int? latencyMs)
    {
        // Intentar cargar MPM.Core.Observability.MpmMetrics via reflection (sin referencia compile-time)
        // Si no está disponible (tests), fallback a Metrics directo si prometheus-net está cargado.
        var asmName = "MPM.Core";
        var typeName = "MPM.Core.Observability.MpmMetrics, " + asmName;
        var metricsType = Type.GetType(typeName);
        if (metricsType == null)
        {
            // Buscar en assemblies ya cargados
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("MPM.Core.Observability.MpmMetrics");
                if (t != null) { metricsType = t; break; }
            }
        }
        if (metricsType == null) return;

        try
        {
            // LlmCallsTotal
            var callsProp = metricsType.GetProperty("LlmCallsTotal");
            var callsCounter = callsProp?.GetValue(null);
            if (callsCounter != null)
            {
                var withLabels = callsCounter.GetType().GetMethod("WithLabels", new[] { typeof(string[]) });
                // prometheus-net Counter.WithLabels(params string[])
                var labeled = withLabels?.Invoke(callsCounter, new object[] { new[] { provider, modelo } });
                labeled?.GetType().GetMethod("Inc", Type.EmptyTypes)?.Invoke(labeled, null);
            }

            // LlmTokensTotal con tipo
            var tokensProp = metricsType.GetProperty("LlmTokensTotal");
            var tokensCounter = tokensProp?.GetValue(null);
            if (tokensCounter != null && promptTokens.HasValue)
            {
                var withLabels = tokensCounter.GetType().GetMethod("WithLabels", new[] { typeof(string[]) });
                var promptLabeled = withLabels?.Invoke(tokensCounter, new object[] { new[] { provider, modelo, "prompt" } });
                promptLabeled?.GetType().GetMethod("Inc", new[] { typeof(double) })?.Invoke(promptLabeled, new object[] { (double)(promptTokens ?? 0) });
                var completionLabeled = withLabels?.Invoke(tokensCounter, new object[] { new[] { provider, modelo, "completion" } });
                completionLabeled?.GetType().GetMethod("Inc", new[] { typeof(double) })?.Invoke(completionLabeled, new object[] { (double)(completionTokens ?? 0) });
            }

            // LlmLatencySeconds (histogram)
            if (latencyMs.HasValue)
            {
                var latencyProp = metricsType.GetProperty("LlmLatencySeconds");
                var latencyHist = latencyProp?.GetValue(null);
                if (latencyHist != null)
                {
                    var withLabels = latencyHist.GetType().GetMethod("WithLabels", new[] { typeof(string[]) });
                    var labeled = withLabels?.Invoke(latencyHist, new object[] { new[] { provider, modelo } });
                    var observe = labeled?.GetType().GetMethod("Observe", new[] { typeof(double) });
                    observe?.Invoke(labeled, new object[] { latencyMs.Value / 1000.0 });
                }
            }
        }
        catch
        {
            // Silenciar - métricas no críticas
        }
    }
}
