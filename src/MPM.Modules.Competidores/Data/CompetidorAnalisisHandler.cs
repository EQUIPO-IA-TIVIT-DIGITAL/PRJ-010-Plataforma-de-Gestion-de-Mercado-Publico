using System.Data;
using System.Text.Json;
using Dapper;
using MPM.Core.Data;
using MPM.Modules.Competidores.Models;

namespace MPM.Modules.Competidores.Data;

public class CompetidorAnalisisHandler(DbConnectionFactory dbFactory)
{
    public async Task<(JsonDocument Contenido, int CantidadLicitaciones)?> BuscarCacheadoAsync(
        string nombreCompetidor, DateOnly fechaDesde, DateOnly fechaHasta, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_nombre_competidor", nombreCompetidor, dbType: DbType.String, size: 300);
        p.Add("p_fecha_desde", fechaDesde.ToDateTime(TimeOnly.MinValue), dbType: DbType.Date);
        p.Add("p_fecha_hasta", fechaHasta.ToDateTime(TimeOnly.MinValue), dbType: DbType.Date);

        var row = await conn.QuerySingleOrDefaultAsync<AnalisisCacheadoRow>(
            CompetidoresStoredProcedures.AnalisisBuscar, p, commandType: CommandType.Text);

        if (row == null) return null;
        return (JsonDocument.Parse(row.p_contenido_json), row.p_cantidad_licitaciones);
    }

    public async Task GuardarAsync(
        string nombreCompetidor, DateOnly fechaDesde, DateOnly fechaHasta,
        string contenidoJson, int cantidadLicitaciones, string usuarioId, CancellationToken ct = default)
    {
        await using var conn = dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_nombre_competidor", nombreCompetidor, dbType: DbType.String, size: 300);
        p.Add("p_fecha_desde", fechaDesde.ToDateTime(TimeOnly.MinValue), dbType: DbType.Date);
        p.Add("p_fecha_hasta", fechaHasta.ToDateTime(TimeOnly.MinValue), dbType: DbType.Date);
        p.Add("p_contenido_json", contenidoJson, dbType: DbType.String);
        p.Add("p_cantidad_licitaciones", cantidadLicitaciones, dbType: DbType.Int32);
        p.Add("p_usuario_id", usuarioId, dbType: DbType.String, size: 100);

        // ON CONFLICT DO NOTHING (definido en el SP, V098) resuelve el edge case de dos usuarios
        // pidiendo el mismo competidor+rango a la vez -- este INSERT puede no persistir nada si
        // alguien ya lo guardo mientras tanto; el llamador relee con BuscarCacheadoAsync.
        await conn.ExecuteAsync(CompetidoresStoredProcedures.AnalisisGuardar, p, commandType: CommandType.Text);
    }
}
