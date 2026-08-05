using System.Data;
using Dapper;
using MPM.Core.Data;
using MPM.Modules.Competidores.Models;

namespace MPM.Modules.Competidores.Data;

public class CompetidoresActividadMercadoHandler(DbConnectionFactory dbFactory)
{
    public virtual async Task<ActividadMercadoCacheRow?> ObtenerCacheAsync(
        string nombreCompetidor, short? area, DateOnly fechaDesde, DateOnly fechaHasta, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_nombre_competidor", nombreCompetidor, DbType.String, size: 300);
        p.Add("p_area_codigo", area, DbType.Int16);
        p.Add("p_fecha_desde", fechaDesde.ToDateTime(TimeOnly.MinValue), DbType.Date);
        p.Add("p_fecha_hasta", fechaHasta.ToDateTime(TimeOnly.MinValue), DbType.Date);

        return await conn.QuerySingleOrDefaultAsync<ActividadMercadoCacheRow>(
            CompetidoresStoredProcedures.ActividadMercadoObtenerCache, p, commandType: CommandType.Text);
    }

    public virtual async Task EncolarAsync(
        string nombreCompetidor, short? area, DateOnly fechaDesde, DateOnly fechaHasta, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_nombre_competidor", nombreCompetidor, DbType.String, size: 300);
        p.Add("p_area_codigo", area, DbType.Int16);
        p.Add("p_fecha_desde", fechaDesde.ToDateTime(TimeOnly.MinValue), DbType.Date);
        p.Add("p_fecha_hasta", fechaHasta.ToDateTime(TimeOnly.MinValue), DbType.Date);

        await conn.ExecuteAsync(CompetidoresStoredProcedures.ActividadMercadoEncolar, p, commandType: CommandType.Text);
    }

    // Lee directo de areas_negocio (tabla compartida, V118) -- igual que Licitaciones ya
    // consulta estados_licitacion sin pasar por el módulo Catalogo, no es una violación del
    // límite de módulos (Principio I), las tablas son infraestructura compartida, no lógica.
    public virtual async Task<string[]> ObtenerPalabrasClaveAreaAsync(short area, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("area", area, DbType.Int16);
        var result = await conn.QuerySingleOrDefaultAsync<string[]>(
            "SELECT palabras_clave FROM areas_negocio WHERE codigo = @area", p, commandType: CommandType.Text);
        return result ?? Array.Empty<string>();
    }
}
