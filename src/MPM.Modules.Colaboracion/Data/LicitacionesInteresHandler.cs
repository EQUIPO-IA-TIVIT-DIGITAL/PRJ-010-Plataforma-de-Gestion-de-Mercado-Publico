using Dapper;
using MPM.Core.Data;
using MPM.Modules.Colaboracion.Models;
using System.Data;

namespace MPM.Modules.Colaboracion.Data;

public class LicitacionesInteresHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public virtual async Task<LicitacionInteresDto> MarcarAsync(long licitacionId, string marcadoPor, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_licitacion_id", licitacionId, DbType.Int64);
        p.Add("p_marcado_por", marcadoPor, DbType.String);

        return await conn.QuerySingleAsync<LicitacionInteresDto>(
            sql: LicitacionesInteresStoredProcedures.Marcar,
            param: p,
            commandType: CommandType.Text);
    }

    public virtual async Task<LicitacionInteresDto?> ObtenerPorLicitacionAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_licitacion_id", licitacionId, DbType.Int64);

        return await conn.QuerySingleOrDefaultAsync<LicitacionInteresDto>(
            sql: LicitacionesInteresStoredProcedures.ObtenerPorLicitacion,
            param: p,
            commandType: CommandType.Text);
    }

    public virtual async Task VincularWorkspaceAsync(long licitacionId, long workspaceId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_licitacion_id", licitacionId, DbType.Int64);
        p.Add("p_workspace_id", workspaceId, DbType.Int64);

        await conn.ExecuteAsync(
            sql: LicitacionesInteresStoredProcedures.VincularWorkspace,
            param: p,
            commandType: CommandType.Text);
    }

    public virtual async Task VincularConversacionAsync(long licitacionId, long conversacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_licitacion_id", licitacionId, DbType.Int64);
        p.Add("p_conversacion_id", conversacionId, DbType.Int64);

        await conn.ExecuteAsync(
            sql: LicitacionesInteresStoredProcedures.VincularConversacion,
            param: p,
            commandType: CommandType.Text);
    }

    public virtual async Task<List<LicitacionInteresListItemDto>> ListarAsync(CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<LicitacionInteresListItemDto>(
            sql: LicitacionesInteresStoredProcedures.Listar,
            commandType: CommandType.Text);
        return result.ToList();
    }
}
