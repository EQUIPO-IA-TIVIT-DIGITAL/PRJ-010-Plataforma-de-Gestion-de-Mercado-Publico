using Prometheus;

namespace MPM.Core.Observability;

/// <summary>
/// 037-B: Metricas Prometheus con prefijo mpm_ . Definicion solamente - el incremento
/// se cableara en 037-C (LlmUsageService, middleware HTTP).
/// OBS-R005: ningun label expone PII (no email, no codigoExterno). Solo provider/modelo/route templada.
/// OBS-R003: llm_* etiquetan provider (gemini|openai) y modelo (gemini-2.5-pro|qwen3.7-g4|...).
/// /metrics solo interno (AllowAnonymous + sin CORS publico), no scrape publico.
/// </summary>
public static class MpmMetrics
{
    // ---------------------------------------------------------------- HTTP

    /// <summary>Contador HTTP por metodo/route/status. Incremento en 037-C via middleware.</summary>
    public static readonly Counter HttpRequestsTotal = Metrics.CreateCounter(
        "mpm_http_requests_total",
        "Total de requests HTTP por metodo, ruta templada y codigo de estado",
        new CounterConfiguration
        {
            LabelNames = new[] { "method", "route", "status" }
        });

    /// <summary>Histograma duracion HTTP en segundos. Buckets tipicos 5ms..10s.</summary>
    public static readonly Histogram HttpDurationSeconds = Metrics.CreateHistogram(
        "mpm_http_duration_seconds",
        "Duracion de requests HTTP en segundos",
        new HistogramConfiguration
        {
            LabelNames = new[] { "method", "route", "status" },
            Buckets = Histogram.ExponentialBuckets(0.005, 2, 12) // 5ms .. ~10s
        });

    // ---------------------------------------------------------------- LLM

    /// <summary>Llamadas LLM totales por provider y modelo. Inc en LlmUsageService (037-C).</summary>
    public static readonly Counter LlmCallsTotal = Metrics.CreateCounter(
        "mpm_llm_calls_total",
        "Total de llamadas a LLM por provider y modelo",
        new CounterConfiguration
        {
            LabelNames = new[] { "provider", "modelo" }
        });

    /// <summary>Tokens totales por provider/modelo/tipo (prompt|completion). Inc en 037-C.</summary>
    public static readonly Counter LlmTokensTotal = Metrics.CreateCounter(
        "mpm_llm_tokens_total",
        "Total de tokens LLM por provider, modelo y tipo",
        new CounterConfiguration
        {
            LabelNames = new[] { "provider", "modelo", "tipo" }
        });

    /// <summary>Latencia LLM en segundos por provider/modelo.</summary>
    public static readonly Histogram LlmLatencySeconds = Metrics.CreateHistogram(
        "mpm_llm_latency_seconds",
        "Latencia de llamadas LLM en segundos por provider y modelo",
        new HistogramConfiguration
        {
            LabelNames = new[] { "provider", "modelo" },
            Buckets = Histogram.ExponentialBuckets(0.05, 2, 10) // 50ms .. ~25s
        });

    // ---------------------------------------------------------------- Negocio / Operativo (definidos para Grafana, Inc en 037-C)

    public static readonly Counter SyncLicitacionesTotal = Metrics.CreateCounter(
        "mpm_sync_licitaciones_total",
        "Total de licitaciones sincronizadas por estado",
        new CounterConfiguration { LabelNames = new[] { "estado" } });

    public static readonly Counter AclaracionesDetectadasTotal = Metrics.CreateCounter(
        "mpm_aclaraciones_detectadas_total",
        "Total de aclaraciones detectadas por el monitor",
        new CounterConfiguration { LabelNames = new[] { "tipo" } });

    public static readonly Counter ScraperRunsTotal = Metrics.CreateCounter(
        "mpm_scraper_runs_total",
        "Total de ejecuciones del scraper por estado",
        new CounterConfiguration { LabelNames = new[] { "estado" } });
}
