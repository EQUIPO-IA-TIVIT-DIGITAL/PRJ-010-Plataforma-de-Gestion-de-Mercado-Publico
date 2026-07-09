using System.Data;
using Dapper;
using MPM.Core.Data;
using MPM.Modules.Competidores.Models;

namespace MPM.Modules.Competidores.Data;

public class OfertasHandler(DbConnectionFactory dbFactory)
{
    public async Task<IEnumerable<OfertaDto>> BuscarPorCompetidorAsync(string nombre, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        var rows = await conn.QueryAsync<OfertaRow>(
            CompetidoresStoredProcedures.BuscarPorCompetidor,
            new { p_nombre = nombre },
            commandType: CommandType.Text);

        return rows.Select(r => new OfertaDto(
            r.p_licitacion_id, r.p_codigo_externo, r.p_nombre_licitacion, r.p_organismo, r.p_fecha_cierre,
            r.p_rut_proveedor, r.p_nombre_proveedor, r.p_monto_oferta, r.p_estado_oferta));
    }

    public async Task<int> ContarPorCompetidorYRangoAsync(string nombre, DateOnly fechaDesde, DateOnly fechaHasta, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_nombre", nombre, dbType: DbType.String, size: 300);
        p.Add("p_fecha_desde", fechaDesde.ToDateTime(TimeOnly.MinValue), dbType: DbType.Date);
        p.Add("p_fecha_hasta", fechaHasta.ToDateTime(TimeOnly.MinValue), dbType: DbType.Date);

        return await conn.QuerySingleAsync<int>(
            CompetidoresStoredProcedures.ContarPorCompetidorYRango, p, commandType: CommandType.Text);
    }

    public async Task<IEnumerable<OfertaDto>> ListarParaAnalisisAsync(string nombre, DateOnly fechaDesde, DateOnly fechaHasta, CancellationToken ct = default)
    {
        // Reusa BuscarPorCompetidor (ya trae todo el historial) y filtra el rango en memoria --
        // el volumen esperado por competidor es chico (decenas/cientos de ofertas, no miles),
        // no amerita un stored procedure nuevo solo para esto.
        var todas = await BuscarPorCompetidorAsync(nombre, ct);
        return todas.Where(o => o.FechaCierre.HasValue
            && DateOnly.FromDateTime(o.FechaCierre.Value) >= fechaDesde
            && DateOnly.FromDateTime(o.FechaCierre.Value) <= fechaHasta);
    }

    public async Task GuardarAsync(long licitacionId, string? rutProveedor, string nombreProveedor, decimal? montoOferta, string? estadoOferta, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_licitacion_id", licitacionId, dbType: DbType.Int64);
        p.Add("p_rut_proveedor", rutProveedor, dbType: DbType.String, size: 20);
        p.Add("p_nombre_proveedor", nombreProveedor, dbType: DbType.String, size: 300);
        p.Add("p_monto_oferta", montoOferta, dbType: DbType.Decimal);
        p.Add("p_estado_oferta", estadoOferta, dbType: DbType.String, size: 30);

        await conn.ExecuteAsync(
            "SELECT usp_LicitacionesOfertas_Guardar(@p_licitacion_id, @p_rut_proveedor, @p_nombre_proveedor, @p_monto_oferta, @p_estado_oferta)",
            p, commandType: CommandType.Text);
    }
}
