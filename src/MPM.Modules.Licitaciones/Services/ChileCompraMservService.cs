using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MPM.Modules.Licitaciones.Services;

/// <summary>
/// Track2 ligero — cliente mserv-datos-abiertos (ADR-016 opción B sin zip).
/// BaseUrl: https://mserv-datos-abiertos.chilecompra.cl/v1
/// Solo lectura agregada; sin retry, throw directo en 429/5xx, timeout 30s.
/// </summary>
public class ChileCompraMservService
{
    public const string BaseUrl = "https://mserv-datos-abiertos.chilecompra.cl/v1";

    private readonly HttpClient _http;
    private readonly ILogger<ChileCompraMservService> _logger;
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChileCompraMservService(HttpClient httpClient, ILogger<ChileCompraMservService> logger)
    {
        _http = httpClient;
        _logger = logger;
        if (_http.Timeout == System.Threading.Timeout.InfiniteTimeSpan || _http.Timeout.TotalSeconds > 35)
            _http.Timeout = TimeSpan.FromSeconds(30);
    }

    // ── DTOs internos ──

    public class ModalityItem
    {
        [JsonPropertyName("idModalidad")]
        public int IdModalidad { get; set; }

        [JsonPropertyName("modalidad")]
        public string Modalidad { get; set; } = string.Empty;

        [JsonPropertyName("amountCLPAnnual")]
        public long AmountCLPAnnual { get; set; }

        [JsonPropertyName("percentageAnnual")]
        public double PercentageAnnual { get; set; }

        [JsonPropertyName("amountUSDAnnual")]
        public long AmountUSDAnnual { get; set; }

        [JsonPropertyName("numberMonthOCAnnual")]
        public int NumberMonthOCAnnual { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("rutSupplier")]
        public string? RutSupplier { get; set; }

        [JsonPropertyName("supplier")]
        public string? Supplier { get; set; }
    }

    public class KpiPayload
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("rutSupplier")]
        public string? RutSupplier { get; set; }

        [JsonPropertyName("supplier")]
        public string? Supplier { get; set; }

        [JsonPropertyName("amountCLP")]
        public long AmountCLP { get; set; }

        [JsonPropertyName("amountUSD")]
        public long AmountUSD { get; set; }

        [JsonPropertyName("rankingAmount")]
        public int RankingAmount { get; set; }

        [JsonPropertyName("numberOCAnnual")]
        public int NumberOCAnnual { get; set; }

        [JsonPropertyName("numberLicAwarded")]
        public int NumberLicAwarded { get; set; }
    }

    public class TradedItem
    {
        [JsonPropertyName("idSector")]
        public int IdSector { get; set; }

        [JsonPropertyName("sector")]
        public string Sector { get; set; } = string.Empty;

        [JsonPropertyName("amountCLPSectorAnnual")]
        public long AmountCLPSectorAnnual { get; set; }

        [JsonPropertyName("percentageSectorAnnual")]
        public double PercentageSectorAnnual { get; set; }
    }

    private class MservEnvelope<T>
    {
        [JsonPropertyName("success")]
        public string? Success { get; set; }

        [JsonPropertyName("payload")]
        public T? Payload { get; set; }

        [JsonPropertyName("errores")]
        public object? Errores { get; set; }

        [JsonPropertyName("trace")]
        public object? Trace { get; set; }
    }

    // ── API ──

    /// <summary>
    /// GET /organismSupplier/modality/{year}/{rut} — lista por modalidad
    /// </summary>
    public async Task<List<ModalityItem>> GetModalityAsync(int year, string rut, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/organismSupplier/modality/{year}/{Uri.EscapeDataString(rut)}";
        return await GetPayloadListAsync<ModalityItem>(url, ct);
    }

    /// <summary>
    /// GET /organismSupplier/getKPI/{year}/{rut}
    /// </summary>
    public async Task<KpiPayload?> GetKpiAsync(int year, string rut, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/organismSupplier/getKPI/{year}/{Uri.EscapeDataString(rut)}";
        return await GetPayloadAsync<KpiPayload>(url, ct);
    }

    /// <summary>
    /// GET /organismSupplier/traded/{year}/{rut}/{modalidadId}
    /// modalidadId 7 = Todos
    /// </summary>
    public async Task<List<TradedItem>> GetTradedAsync(int year, string rut, int modalidadId = 7, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/organismSupplier/traded/{year}/{Uri.EscapeDataString(rut)}/{modalidadId}";
        return await GetPayloadListAsync<TradedItem>(url, ct);
    }

    /// <summary>
    /// Atajo: monto Convenio Marco (idModalidad=5) para un año. 0 si no hay.
    /// </summary>
    public async Task<long> GetConvenioMarcoAnualAsync(string rut, int year, CancellationToken ct = default)
    {
        var list = await GetModalityAsync(year, rut, ct);
        return list.FirstOrDefault(x => x.IdModalidad == 5)?.AmountCLPAnnual ?? 0L;
    }

    private async Task<T?> GetPayloadAsync<T>(string url, CancellationToken ct) where T : class
    {
        _logger.LogDebug("mserv GET {Url}", url);
        var response = await _http.GetAsync(url, ct);

        if ((int)response.StatusCode == 429)
            throw new HttpRequestException("429 Too Many Requests from mserv", null, System.Net.HttpStatusCode.TooManyRequests);

        // 5xx throw directo, sin retry
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var envelope = JsonSerializer.Deserialize<MservEnvelope<T>>(json, _jsonOpts);
        return envelope?.Payload;
    }

    private async Task<List<T>> GetPayloadListAsync<T>(string url, CancellationToken ct) where T : class
    {
        _logger.LogDebug("mserv GET {Url}", url);
        var response = await _http.GetAsync(url, ct);

        if ((int)response.StatusCode == 429)
            throw new HttpRequestException("429 Too Many Requests from mserv", null, System.Net.HttpStatusCode.TooManyRequests);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var envelope = JsonSerializer.Deserialize<MservEnvelope<List<T>>>(json, _jsonOpts);
        return envelope?.Payload ?? new List<T>();
    }
}
