using Dapper;
using MPM.Core.Data;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Licitaciones.Services;

public class SyncLogHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<long> IniciarSync(string tipo, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_tipo", tipo);
        p.Add("p_sync_id", 0, dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        p.Add("p_error_msg", null, dbType: DbType.String, direction: ParameterDirection.InputOutput, size: 4000);

        await conn.ExecuteAsync(
            sql: "CALL usp_SyncLog_Iniciar(@p_tipo, @p_sync_id, @p_error_msg)",
            param: p, commandType: CommandType.Text);

        var errorMsg = p.Get<string?>("p_error_msg");
        if (!string.IsNullOrEmpty(errorMsg))
            throw new InvalidOperationException(errorMsg);

        return p.Get<long>("p_sync_id");
    }

    public async Task<bool> ExisteTipo(string tipo, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var existe = await conn.ExecuteScalarAsync<bool>(
            sql: "SELECT p_existe FROM usp_SyncLog_ExisteTipo(@p_tipo)",
            param: new { p_tipo = tipo });
        return existe;
    }

    public async Task FinalizarSync(long syncId, int creados, int actualizados, int eliminados,
        int errores, string? detalleErroresJson, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_sync_id", syncId);
        p.Add("p_creados", creados);
        p.Add("p_actualizados", actualizados);
        p.Add("p_eliminados", eliminados);
        p.Add("p_errores", errores);
        p.Add("p_detalle_errores", detalleErroresJson);
        p.Add("p_error_msg", null, dbType: DbType.String, direction: ParameterDirection.InputOutput, size: 4000);

        await conn.ExecuteAsync(
            sql: "CALL usp_SyncLog_Finalizar(@p_sync_id, @p_creados, @p_actualizados, @p_eliminados, @p_errores, @p_detalle_errores, @p_error_msg)",
            param: p, commandType: CommandType.Text);

        var errorMsg = p.Get<string?>("p_error_msg");
        if (!string.IsNullOrEmpty(errorMsg))
            throw new InvalidOperationException(errorMsg);
    }
}
