using Dapper;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Models;
using Npgsql;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Licitaciones.Data;

public class LicitacionHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<(System.Collections.Generic.List<LicitacionResumenDto> items, int totalCount)> ListarAsync(
        int page, int pageSize, string? search, short? estado, string? tipo, string? organismo,
        DateTime? fechaDesde, DateTime? fechaHasta, string sortBy, string sortDir,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<LicitacionResumenDto>(
            sql: LicitacionStoredProcedures.Listar,
            param: new
            {
                p_page = page,
                p_page_size = pageSize,
                p_search = search,
                p_estado = estado,
                p_tipo = tipo,
                p_organismo = organismo,
                p_fecha_desde = fechaDesde,
                p_fecha_hasta = fechaHasta,
                p_sort_by = sortBy,
                p_sort_dir = sortDir
            },
            commandType: CommandType.Text);

        var list = result.ToList();
        var totalCount = list.Count > 0 ? list[0].TotalCount : 0;

        return (list, totalCount);
    }

    public async Task<LicitacionDetalleDto?> ObtenerPorCodigoAsync(string codigoExterno, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<ObtenerResult>(
            sql: LicitacionStoredProcedures.Obtener,
            param: new { p_codigo_externo = codigoExterno },
            commandType: CommandType.Text);

        if (row?.licitacion == null) return null;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var licitacionJson = row.licitacion;
        var itemsJson = row.items;

        var doc = JsonDocument.Parse(licitacionJson);
        var root = doc.RootElement;

        var dto = new LicitacionDetalleDto
        {
            CodigoExterno = root.TryGetProperty("codigo_externo", out var ce) ? ce.GetString() ?? "" : "",
            Nombre = root.TryGetProperty("nombre", out var n) ? n.GetString() ?? "" : "",
            Tipo = root.TryGetProperty("tipo", out var t) ? t.GetString() ?? "" : "",
            CodigoEstado = root.TryGetProperty("codigo_estado", out var est) && est.ValueKind == JsonValueKind.Number ? (short)est.GetInt32() : (short)0,
            EstadoNombre = root.TryGetProperty("codigo_estado", out var est2) && est2.ValueKind == JsonValueKind.Number
                ? await GetEstadoNombreAsync(conn, (short)est2.GetInt32()) : "",
            Descripcion = root.TryGetProperty("descripcion", out var desc) && desc.ValueKind != JsonValueKind.Null ? desc.GetString() : null,
            UnidadTecnica = root.TryGetProperty("unidad_tecnica", out var ut) && ut.ValueKind != JsonValueKind.Null ? ut.GetString() : null,
            Organismo = root.TryGetProperty("organismo", out var org) && org.ValueKind != JsonValueKind.Null ? org.GetString() : null,
            Moneda = root.TryGetProperty("moneda", out var mon) && mon.ValueKind != JsonValueKind.Null ? mon.GetString() ?? "CLP" : "CLP",
            Link = root.TryGetProperty("link", out var ln) && ln.ValueKind != JsonValueKind.Null ? ln.GetString() : null,
            MontoEstimado = root.TryGetProperty("monto_estimado", out var monto) && monto.ValueKind != JsonValueKind.Null && monto.TryGetDecimal(out var md) ? md : null,
            FechaPublicacion = root.TryGetProperty("fecha_publicacion", out var fp) && fp.ValueKind != JsonValueKind.Null && DateTime.TryParse(fp.GetString(), out var fpd) ? fpd : null,
            FechaCierre = root.TryGetProperty("fecha_cierre", out var fc) && fc.ValueKind != JsonValueKind.Null && DateTime.TryParse(fc.GetString(), out var fcd) ? fcd : null,
            FechaAdjudicacion = root.TryGetProperty("fecha_adjudicacion", out var fa) && fa.ValueKind != JsonValueKind.Null && DateTime.TryParse(fa.GetString(), out var fad) ? fad : null,
            FechaEstimadaAdjudicacion = root.TryGetProperty("fecha_estimada_adjudicacion", out var fea) && fea.ValueKind != JsonValueKind.Null && DateTime.TryParse(fea.GetString(), out var fead) ? fead : null,
        };

        if (!string.IsNullOrEmpty(itemsJson))
        {
            var itemsDoc = JsonDocument.Parse(itemsJson);
            if (itemsDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsDoc.RootElement.EnumerateArray())
                {
                    dto.Items.Add(new LicitacionItemDto
                    {
                        Codigo = item.TryGetProperty("codigo", out var ic) && ic.ValueKind == JsonValueKind.Number ? ic.GetInt32() : 0,
                        Nombre = item.TryGetProperty("nombre", out var inm) ? inm.GetString() ?? "" : "",
                        Cantidad = item.TryGetProperty("cantidad", out var iq) && iq.ValueKind == JsonValueKind.Number ? iq.GetInt32() : null,
                        UnidadMedida = item.TryGetProperty("unidad_medida", out var ium) && ium.ValueKind != JsonValueKind.Null ? ium.GetString() : null,
                        PrecioEstimado = item.TryGetProperty("precio_estimado", out var ipre) && ipre.ValueKind != JsonValueKind.Null && ipre.TryGetDecimal(out var ipd) ? ipd : null,
                        Categoria = item.TryGetProperty("categoria", out var icat) && icat.ValueKind != JsonValueKind.Null ? icat.GetString() : null,
                    });
                }
            }
        }

        return dto;
    }

    private async Task<string> GetEstadoNombreAsync(Npgsql.NpgsqlConnection conn, short codigo)
    {
        var nombre = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT nombre FROM estados_licitacion WHERE codigo = @codigo",
            new { codigo });
        return nombre ?? "";
    }

    public async Task<System.Collections.Generic.List<LicitacionSearchResult>> BuscarAsync(string search, int limit, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<LicitacionSearchResult>(
            sql: LicitacionStoredProcedures.Buscar,
            param: new { p_q = search, p_limit = limit },
            commandType: CommandType.Text);

        return result.ToList();
    }

    public async Task ActualizarDetalleAsync(string codigoExterno, LicitacionDetalleDto dto, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(@"
            UPDATE licitaciones SET
                descripcion = @descripcion,
                organismo = @organismo,
                unidad_tecnica = @unidad_tecnica,
                moneda = @moneda,
                monto_estimado = @monto_estimado,
                fecha_publicacion = @fecha_publicacion,
                fecha_adjudicacion = @fecha_adjudicacion,
                fecha_estimada_adjudicacion = @fecha_estimada_adjudicacion,
                link = @link,
                tipo = @tipo,
                updated_at = CURRENT_TIMESTAMP
            WHERE codigo_externo = @codigo_externo AND deleted_at IS NULL",
            new
            {
                codigo_externo = codigoExterno,
                dto.Descripcion,
                dto.Organismo,
                dto.UnidadTecnica,
                dto.Moneda,
                dto.MontoEstimado,
                dto.FechaPublicacion,
                dto.FechaAdjudicacion,
                dto.FechaEstimadaAdjudicacion,
                dto.Link,
                dto.Tipo
            });
    }

    public async Task<(List<LicitacionNaturalSearchResult> Items, long TotalCount)> BuscarNaturalAsync(
        string query, int page, int pageSize, short? estado, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        var items = await conn.QueryAsync<LicitacionNaturalSearchResult>(
            sql: LicitacionStoredProcedures.BuscarNatural,
            param: new
            {
                p_query = query,
                p_page = page,
                p_page_size = pageSize,
                p_estado = estado,
                p_fecha_desde = "2026-01-01",
            },
            commandType: CommandType.Text);

        var totalCount = await conn.QueryFirstOrDefaultAsync<long>(
            sql: LicitacionStoredProcedures.BuscarNaturalCount,
            param: new
            {
                p_query = query,
                p_estado = estado,
                p_fecha_desde = "2026-01-01",
            },
            commandType: CommandType.Text);

        var list = items.ToList();
        return (list, totalCount);
    }

    private class ObtenerResult
    {
        public string? licitacion { get; set; }
        public string? items { get; set; }
    }
}
