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

    /// <summary>
    /// Licitaciones publicadas desde <paramref name="fechaDesde"/>, con los campos mínimos
    /// que necesita el motor de matching de Alertas (003-fase6-alertas-keywords) — separado
    /// de <see cref="ListarAsync"/> para no acoplar ese caso de uso al del frontend.
    /// </summary>
    public async Task<IEnumerable<MPM.Modules.Alertas.Models.LicitacionParaMatching>> ListarParaMatchingAsync(
        DateTime fechaDesde, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<MatchingRow>(
            LicitacionStoredProcedures.ListarParaMatching,
            new { p_fecha_desde = fechaDesde },
            commandType: CommandType.Text);

        return rows.Select(r => new MPM.Modules.Alertas.Models.LicitacionParaMatching(
            r.p_id, r.p_codigo_externo, r.p_nombre, r.p_descripcion, r.p_monto_estimado, r.p_tipo, r.p_organismo));
    }

    private class MatchingRow
    {
        public long p_id { get; set; }
        public string p_codigo_externo { get; set; } = "";
        public string p_nombre { get; set; } = "";
        public string? p_descripcion { get; set; }
        public decimal? p_monto_estimado { get; set; }
        public string? p_tipo { get; set; }
        public string? p_organismo { get; set; }
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
        // DynamicParameters con DbType explicito por parametro: el binding via objeto anonimo
        // dejaba parametros con valor null (unidad_tecnica, monto_estimado, fechas) sin
        // sustituir en el texto SQL enviado a Postgres -- "operator does not exist: @
        // character varying", porque Npgsql no podia inferir el tipo de un valor null sin
        // tipo explicito.
        var p = new DynamicParameters();
        p.Add("codigo_externo", codigoExterno, DbType.String);
        p.Add("descripcion", dto.Descripcion, DbType.String);
        p.Add("organismo", dto.Organismo, DbType.String);
        p.Add("unidad_tecnica", dto.UnidadTecnica, DbType.String);
        p.Add("moneda", dto.Moneda, DbType.String);
        p.Add("monto_estimado", dto.MontoEstimado, DbType.Decimal);
        // DbType.DateTime2 (no DateTime) porque en Npgsql moderno DbType.DateTime apunta a
        // "timestamp with time zone" y estas columnas son "timestamp without time zone";
        // con DateTime.Kind=Unspecified (que es lo que trae la API de MP) eso revienta con
        // "Cannot write DateTime with Kind=Unspecified to ... timestamp with time zone".
        p.Add("fecha_publicacion", dto.FechaPublicacion, DbType.DateTime2);
        p.Add("fecha_adjudicacion", dto.FechaAdjudicacion, DbType.DateTime2);
        p.Add("fecha_estimada_adjudicacion", dto.FechaEstimadaAdjudicacion, DbType.DateTime2);
        p.Add("link", dto.Link, DbType.String);
        p.Add("tipo", dto.Tipo, DbType.String);

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
            p);
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
