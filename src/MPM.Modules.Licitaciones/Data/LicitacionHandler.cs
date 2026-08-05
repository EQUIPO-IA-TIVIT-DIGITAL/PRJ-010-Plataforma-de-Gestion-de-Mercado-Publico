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

    // virtual para poder mockearlo en LicitacionServiceTests (mismo patrón que BuscarNaturalAsync)
    public virtual async Task<(System.Collections.Generic.List<LicitacionResumenDto> items, int totalCount)> ListarAsync(
        int page, int pageSize, string? search, short? estado, string? tipo, string? organismo,
        DateTime? fechaDesde, DateTime? fechaHasta, string sortBy, string sortDir,
        short? area = null, bool? sinClasificar = null,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        // 029-fix-hallazgos-code-review-competidores-alertas (FR-009/QA BUG-002): p_fecha_desde/
        // p_fecha_hasta iban en un objeto anonimo sin tipo explicito -- un DateTime? en null
        // llega a Postgres como parametro "unknown" (Npgsql no puede inferir entre date/
        // timestamp/timestamptz sin tipo explicito), y usp_Licitaciones_Listar (V093) declara
        // esos parametros como DATE, asi que Postgres no resolvia el overload (42883, 500 en
        // cada filtro de fecha). Mismo patron ya usado en BuscarNaturalAsync/ActualizarDetalleAsync
        // en este mismo archivo.
        var p = new DynamicParameters();
        p.Add("p_page", page, DbType.Int32);
        p.Add("p_page_size", pageSize, DbType.Int32);
        p.Add("p_search", search, DbType.String);
        p.Add("p_estado", estado, DbType.Int16);
        p.Add("p_tipo", tipo, DbType.String);
        p.Add("p_organismo", organismo, DbType.String);
        p.Add("p_fecha_desde", fechaDesde, DbType.Date);
        p.Add("p_fecha_hasta", fechaHasta, DbType.Date);
        p.Add("p_sort_by", sortBy, DbType.String);
        p.Add("p_sort_dir", sortDir, DbType.String);
        p.Add("p_area", area, DbType.Int16);
        p.Add("p_sin_clasificar", sinClasificar, DbType.Boolean);

        var result = await conn.QueryAsync<LicitacionResumenDto>(
            sql: LicitacionStoredProcedures.Listar,
            param: p,
            commandType: CommandType.Text);

        var list = result.ToList();
        var totalCount = list.Count > 0 ? list[0].TotalCount : 0;

        return (list, totalCount);
    }

    // US2 (spec 031): estadísticas de licitaciones por estado, con el mismo filtro de
    // área que ListarAsync (fn_licitacion_area_codigos, V118/V121) para que el drill-down
    // navegue a un listado con conteos consistentes.
    public virtual async Task<System.Collections.Generic.List<EstadoConteoDto>> ContarPorEstadoAsync(
        short? area, bool? sinClasificar, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        var p = new DynamicParameters();
        p.Add("p_area", area, DbType.Int16);
        p.Add("p_sin_clasificar", sinClasificar, DbType.Boolean);

        var result = await conn.QueryAsync<EstadoConteoDto>(
            sql: LicitacionStoredProcedures.ContarPorEstado,
            param: p,
            commandType: CommandType.Text);

        return result.ToList();
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

    /// <summary>
    /// 029-fix-hallazgos-code-review-competidores-alertas (FR-010): licitaciones con tipo
    /// genérico ("Licitacion") o nulo, candidatas a re-derivar su tipo real por sufijo de
    /// codigo_externo -- ver ImportBackfillService.
    /// </summary>
    public async Task<IEnumerable<string>> ListarParaBackfillTipoAsync(int limite, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<BackfillTipoRow>(
            LicitacionStoredProcedures.ListarParaBackfillTipo,
            new { p_limite = limite },
            commandType: CommandType.Text);
        return rows.Select(r => r.codigo_externo);
    }

    private class BackfillTipoRow
    {
        public string codigo_externo { get; set; } = "";
        public string? tipo_actual { get; set; }
    }

    public async Task ActualizarTipoBackfillAsync(string codigoExterno, string tipo, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(
            LicitacionStoredProcedures.ActualizarTipoBackfill,
            new { p_codigo_externo = codigoExterno, p_tipo = tipo },
            commandType: CommandType.Text);
    }

    /// <summary>
    /// 029-fix-hallazgos-code-review-competidores-alertas (FR-010): licitaciones que cumplen el
    /// mismo trigger que ya usa <c>LicitacionService.ObtenerPorCodigoAsync</c> on-demand
    /// (descripcion vacía Y fecha_publicacion nula), candidatas al backfill de organismo vía API
    /// real de Mercado Público -- ver ImportBackfillService.
    /// </summary>
    public async Task<IEnumerable<string>> ListarParaBackfillOrganismoAsync(int limite, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<string>(
            LicitacionStoredProcedures.ListarParaBackfillOrganismo,
            new { p_limite = limite },
            commandType: CommandType.Text);
        return rows;
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
            // spec 031 (US5): faltaba mapear Id -- nadie lo necesitaba hasta que
            // LicitacionInteresPanel empezó a usar el id numérico de la licitación mostrada en
            // el drawer de detalle (encontrado en vivo vía el E2E de Playwright: todas las
            // llamadas terminaban en /licitaciones/0/interes).
            Id = root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64() : 0,
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

    // virtual para poder mockearlo en LicitacionServiceTests
    public virtual async Task<(List<LicitacionNaturalSearchResult> Items, long TotalCount)> BuscarNaturalAsync(
        string query, int page, int pageSize, short? estado,
        List<string>? terminosExpandidos = null, decimal? montoDesde = null, decimal? montoHasta = null,
        DateTime? fechaDesde = null, DateTime? fechaHasta = null, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        // DynamicParameters con DbType.Date explicito para p_fecha_desde/p_fecha_hasta -- igual
        // que ActualizarDetalleAsync (ver comentario ahi), un objeto anonimo con un DateTime? en
        // null llega a Postgres como parametro "unknown" (42883, no resuelve el overload de la
        // funcion) porque Npgsql no puede inferir entre date/timestamp/timestamptz sin tipo
        // explicito. p_estado/p_monto_desde/p_monto_hasta no mostraban este problema en la
        // practica, pero se tipan explicito tambien para no repetir el bug si cambian de tipo.
        //
        // 029-fix-hallazgos-code-review-competidores-alertas (FR-002): antes, p_fecha_desde
        // estaba hardcodeado a 2026-01-01 en vez de recibir el valor real inferido por
        // ConsultaSemanticaService -- toda busqueda NL de un periodo anterior a esa fecha
        // devolvia una lista vacia sin explicacion. Si fechaDesde es null (Gemini no infirio una
        // fecha de inicio), no se acota el rango por abajo -- igual que ya hace fechaHasta.
        var itemsParams = new DynamicParameters();
        itemsParams.Add("p_query", query, DbType.String);
        itemsParams.Add("p_page", page, DbType.Int32);
        itemsParams.Add("p_page_size", pageSize, DbType.Int32);
        itemsParams.Add("p_estado", estado, DbType.Int16);
        itemsParams.Add("p_fecha_desde", fechaDesde, DbType.Date);
        itemsParams.Add("p_terminos_expandidos", terminosExpandidos?.ToArray());
        itemsParams.Add("p_monto_desde", montoDesde, DbType.Decimal);
        itemsParams.Add("p_monto_hasta", montoHasta, DbType.Decimal);
        itemsParams.Add("p_fecha_hasta", fechaHasta, DbType.Date);

        var items = await conn.QueryAsync<LicitacionNaturalSearchResult>(
            sql: LicitacionStoredProcedures.BuscarNatural,
            param: itemsParams,
            commandType: CommandType.Text);

        var countParams = new DynamicParameters();
        countParams.Add("p_query", query, DbType.String);
        countParams.Add("p_estado", estado, DbType.Int16);
        countParams.Add("p_fecha_desde", fechaDesde, DbType.Date);
        countParams.Add("p_terminos_expandidos", terminosExpandidos?.ToArray());
        countParams.Add("p_monto_desde", montoDesde, DbType.Decimal);
        countParams.Add("p_monto_hasta", montoHasta, DbType.Decimal);
        countParams.Add("p_fecha_hasta", fechaHasta, DbType.Date);

        var totalCount = await conn.QueryFirstOrDefaultAsync<long>(
            sql: LicitacionStoredProcedures.BuscarNaturalCount,
            param: countParams,
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
