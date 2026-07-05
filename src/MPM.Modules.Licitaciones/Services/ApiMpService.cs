using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MPM.Modules.Licitaciones.Services;

public class ApiMpService(HttpClient httpClient, ILogger<ApiMpService> logger)
{
    public async Task<List<LicitacionRawDto>> GetLicitacionesDelDiaAsync(DateTime date, string ticket, CancellationToken ct = default)
    {
        string dateString = date.ToString("ddMMyyyy");
        string url = $"https://api.mercadopublico.cl/servicios/v1/publico/licitaciones.json?ticket={ticket}&fecha={dateString}";

        var response = await httpClient.GetAsync(url, ct);

        if ((int)response.StatusCode == 429)
            throw new HttpRequestException("429 Too Many Requests");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var apiResponse = JsonSerializer.Deserialize<ApiMpListResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (apiResponse?.Listado == null)
            return new List<LicitacionRawDto>();

        return apiResponse.Listado.Select(MapToLicitacionRaw).ToList();
    }

    public async Task<ApiMpLicitacion?> GetDetalleAsync(string codigo, string ticket, CancellationToken ct = default)
    {
        string url = $"https://api.mercadopublico.cl/servicios/v1/publico/licitaciones.json?ticket={ticket}&codigo={codigo}";

        var response = await httpClient.GetAsync(url, ct);

        if ((int)response.StatusCode == 429)
            throw new HttpRequestException("429 Too Many Requests");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var apiResponse = JsonSerializer.Deserialize<ApiMpListResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return apiResponse?.Listado?.FirstOrDefault();
    }

    private static LicitacionRawDto MapToLicitacionRaw(ApiMpLicitacion item) => new()
    {
        codigo_externo = item.CodigoExterno,
        nombre = item.Nombre,
        descripcion = null,
        codigo_estado = ParseCodigoEstado(item.CodigoEstado),
        tipo = "Licitacion",
        organismo = null,
        unidad_tecnica = null,
        moneda = "CLP",
        monto_estimado = null,
        fecha_publicacion = null,
        fecha_cierre = DateTime.TryParse(item.FechaCierre, out var fc) ? fc : null,
        fecha_adjudicacion = null,
        fecha_estimada_adjudicacion = null,
        link = null,
        raw_data = JsonSerializer.Serialize(item)
    };

    private static short ParseCodigoEstado(JsonElement? el)
    {
        if (el == null) return 1;
        return el.Value.ValueKind switch
        {
            JsonValueKind.Number => (short)el.Value.GetInt32(),
            JsonValueKind.String => short.TryParse(el.Value.GetString(), out var v) ? v : (short)1,
            _ => (short)1
        };
    }
}

public class ApiMpListResponse
{
    [JsonPropertyName("Listado")]
    public List<ApiMpLicitacion>? Listado { get; set; }
}

public class ApiMpLicitacion
{
    [JsonPropertyName("CodigoExterno")]
    public string? CodigoExterno { get; set; }

    [JsonPropertyName("Nombre")]
    public string? Nombre { get; set; }

    [JsonPropertyName("CodigoEstado")]
    public JsonElement? CodigoEstado { get; set; }

    [JsonPropertyName("FechaCierre")]
    public string? FechaCierre { get; set; }

    [JsonPropertyName("Descripcion")]
    public string? Descripcion { get; set; }

    [JsonPropertyName("Tipo")]
    public string? Tipo { get; set; }

    [JsonPropertyName("Moneda")]
    public string? Moneda { get; set; }

    [JsonPropertyName("MontoEstimado")]
    public decimal? MontoEstimado { get; set; }

    [JsonPropertyName("Comprador")]
    public ApiMpComprador? Comprador { get; set; }

    [JsonPropertyName("Fechas")]
    public ApiMpFechas? Fechas { get; set; }

    [JsonPropertyName("Items")]
    public JsonElement? ItemsRaw { get; set; }

    [JsonIgnore]
    public List<ApiMpItem>? Items => ParseItems(ItemsRaw);

    [JsonPropertyName("Preguntas")]
    public ApiMpPreguntas? Preguntas { get; set; }

    private static List<ApiMpItem>? ParseItems(JsonElement? el)
    {
        if (el == null) return null;
        if (el.Value.ValueKind == JsonValueKind.Array)
            return el.Value.Deserialize<List<ApiMpItem>>();
        if (el.Value.ValueKind == JsonValueKind.Object && el.Value.TryGetProperty("Listado", out var listado))
            return listado.Deserialize<List<ApiMpItem>>();
        return null;
    }
}

public class ApiMpComprador
{
    [JsonPropertyName("CodigoOrganismo")]
    public string? CodigoOrganismo { get; set; }

    [JsonPropertyName("NombreOrganismo")]
    public string? NombreOrganismo { get; set; }

    [JsonPropertyName("NombreUnidad")]
    public string? NombreUnidad { get; set; }
}

public class ApiMpFechas
{
    [JsonPropertyName("FechaPublicacion")]
    public string? FechaPublicacion { get; set; }

    [JsonPropertyName("FechaCierre")]
    public string? FechaCierre { get; set; }

    [JsonPropertyName("FechaAdjudicacion")]
    public string? FechaAdjudicacion { get; set; }

    [JsonPropertyName("FechaEstimadaAdjudicacion")]
    public string? FechaEstimadaAdjudicacion { get; set; }
}

public class ApiMpItem
{
    [JsonPropertyName("Correlativo")]
    public int? Correlativo { get; set; }

    [JsonPropertyName("CodigoProducto")]
    public long? CodigoProducto { get; set; }

    [JsonPropertyName("NombreProducto")]
    public string? NombreProducto { get; set; }

    [JsonPropertyName("Cantidad")]
    public decimal? Cantidad { get; set; }

    [JsonPropertyName("UnidadMedida")]
    public string? UnidadMedida { get; set; }

    [JsonPropertyName("Categoria")]
    public string? Categoria { get; set; }

    [JsonPropertyName("Adjudicacion")]
    public ApiMpAdjudicacionItem? Adjudicacion { get; set; }
}

public class ApiMpAdjudicacionItem
{
    [JsonPropertyName("MontoUnitario")]
    public decimal? MontoUnitario { get; set; }
}

public class ApiMpPreguntas
{
    [JsonPropertyName("Listado")]
    public List<ApiMpAclaracion>? Listado { get; set; }
}

public class ApiMpAclaracion
{
    [JsonPropertyName("CodigoAclaracion")]
    public int CodigoAclaracion { get; set; }

    [JsonPropertyName("Pregunta")]
    public string? Pregunta { get; set; }

    [JsonPropertyName("Respuesta")]
    public string? Respuesta { get; set; }

    [JsonPropertyName("FechaPublicacion")]
    public string? FechaPublicacion { get; set; }

    [JsonPropertyName("FechaRespuesta")]
    public string? FechaRespuesta { get; set; }
}

public class ApiMpDetalleResponse
{
    [JsonPropertyName("CodigoExterno")]
    public string? CodigoExterno { get; set; }

    [JsonPropertyName("Nombre")]
    public string? Nombre { get; set; }

    [JsonPropertyName("CodigoEstado")]
    public JsonElement? CodigoEstado { get; set; }

    [JsonPropertyName("FechaCierre")]
    public string? FechaCierre { get; set; }

    [JsonPropertyName("Descripcion")]
    public string? Descripcion { get; set; }

    [JsonPropertyName("Tipo")]
    public string? Tipo { get; set; }

    [JsonPropertyName("Moneda")]
    public string? Moneda { get; set; }

    [JsonPropertyName("MontoEstimado")]
    public decimal? MontoEstimado { get; set; }

    [JsonPropertyName("Comprador")]
    public ApiMpComprador? Comprador { get; set; }

    [JsonPropertyName("Fechas")]
    public ApiMpFechas? Fechas { get; set; }

    [JsonPropertyName("Items")]
    public List<ApiMpItem>? Items { get; set; }
}

public class LicitacionRawDto
{
    public string codigo_externo { get; set; } = string.Empty;
    public string nombre { get; set; } = string.Empty;
    public string? descripcion { get; set; }
    public short codigo_estado { get; set; }
    public string tipo { get; set; } = string.Empty;
    public string? organismo { get; set; }
    public string? unidad_tecnica { get; set; }
    public string moneda { get; set; } = "CLP";
    public decimal? monto_estimado { get; set; }
    public DateTime? fecha_publicacion { get; set; }
    public DateTime? fecha_cierre { get; set; }
    public DateTime? fecha_adjudicacion { get; set; }
    public DateTime? fecha_estimada_adjudicacion { get; set; }
    public string? link { get; set; }
    public string? raw_data { get; set; }
}
