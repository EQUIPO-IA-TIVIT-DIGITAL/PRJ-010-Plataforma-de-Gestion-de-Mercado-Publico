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
        ValidarRespuestaJson(json);
        var apiResponse = JsonSerializer.Deserialize<ApiMpListResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (apiResponse?.Listado == null)
            return new List<LicitacionRawDto>();

        return apiResponse.Listado.Select(item => MapToLicitacionRaw(item, date)).ToList();
    }

    public async Task<ApiMpLicitacion?> GetDetalleAsync(string codigo, string ticket, CancellationToken ct = default)
    {
        string url = $"https://api.mercadopublico.cl/servicios/v1/publico/licitaciones.json?ticket={ticket}&codigo={codigo}";

        var response = await httpClient.GetAsync(url, ct);

        if ((int)response.StatusCode == 429)
            throw new HttpRequestException("429 Too Many Requests");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        ValidarRespuestaJson(json);
        var apiResponse = JsonSerializer.Deserialize<ApiMpListResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return apiResponse?.Listado?.FirstOrDefault();
    }

    private static LicitacionRawDto MapToLicitacionRaw(ApiMpLicitacion item, DateTime? defaultFechaPublicacion = null) => new()
    {
        codigo_externo = item.CodigoExterno,
        nombre = item.Nombre,
        descripcion = item.Descripcion,
        codigo_estado = ParseCodigoEstado(item.CodigoEstado),
        tipo = ParseTipoDesdeCodigo(item.CodigoExterno),
        organismo = item.Comprador?.NombreOrganismo,
        unidad_tecnica = item.Comprador?.NombreUnidad,
        moneda = item.Moneda ?? "CLP",
        monto_estimado = item.MontoEstimado,
        fecha_publicacion = DateTime.TryParse(item.Fechas?.FechaPublicacion, out var fp) ? fp : defaultFechaPublicacion,
        fecha_cierre = DateTime.TryParse(item.FechaCierre, out var fc) ? fc : null,
        fecha_adjudicacion = DateTime.TryParse(item.Fechas?.FechaAdjudicacion, out var fa) ? fa : null,
        fecha_estimada_adjudicacion = DateTime.TryParse(item.Fechas?.FechaEstimadaAdjudicacion, out var fea) ? fea : null,
        link = $"https://www.mercadopublico.cl/Procurement/Modules/RFB/DetailsAcquisition.aspx?qs=/{item.CodigoExterno}",
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

    private static void ValidarRespuestaJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("Codigo", out var codigoProp))
            {
                int codigoVal = 0;
                if (codigoProp.ValueKind == JsonValueKind.Number)
                {
                    codigoVal = codigoProp.GetInt32();
                }
                else if (codigoProp.ValueKind == JsonValueKind.String)
                {
                    int.TryParse(codigoProp.GetString(), out codigoVal);
                }

                if (codigoVal > 200)
                {
                    var mensaje = root.TryGetProperty("Mensaje", out var msgProp) ? msgProp.GetString() : "Error de la API de Mercado Publico";
                    throw new HttpRequestException($"API Error {codigoVal}: {mensaje} (Simulando 429 para reintento)", null, System.Net.HttpStatusCode.TooManyRequests);
                }
            }
        }
    }

    // 029-fix-hallazgos-code-review-competidores-alertas (FR-010): internal (no private) para que
    // ImportBackfillService reutilice la misma derivación de tipo por sufijo, en vez de duplicarla.
    internal static string ParseTipoDesdeCodigo(string? codigoExterno)
    {
        if (string.IsNullOrWhiteSpace(codigoExterno)) return "Licitacion";
        var partes = codigoExterno.Split('-');
        if (partes.Length < 3) return "Licitacion";

        var parte3 = partes[2];
        var letras = new string(parte3.TakeWhile(char.IsLetter).ToArray());

        if (string.IsNullOrWhiteSpace(letras)) return "Licitacion";
        return letras.ToUpper();
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
