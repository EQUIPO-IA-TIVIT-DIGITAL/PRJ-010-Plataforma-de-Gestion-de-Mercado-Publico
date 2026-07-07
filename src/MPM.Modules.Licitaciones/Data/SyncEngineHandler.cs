using Dapper;
using Microsoft.Extensions.Logging;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Services;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Licitaciones.Data;

public class SyncEngineHandler(DbConnectionFactory dbFactory, ILogger<SyncEngineHandler> logger)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<(int creados, int actualizados)> MergeLicitacionesAsync(
        List<LicitacionRawDto> licitaciones, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(licitaciones);
        var p = new DynamicParameters();
        p.Add("p_datos", json);
        p.Add("p_creados", 0, dbType: DbType.Int32, direction: ParameterDirection.InputOutput);
        p.Add("p_actualizados", 0, dbType: DbType.Int32, direction: ParameterDirection.InputOutput);
        p.Add("p_error_msg", null, dbType: DbType.String, direction: ParameterDirection.InputOutput, size: 4000);

        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync(
            sql: LicitacionStoredProcedures.MergeLicitaciones,
            param: p,
            commandType: CommandType.Text);

        // El procedimiento captura errores por item (uno invalido no debe perder el resto del
        // lote); se loguean como advertencia en vez de abortar el dia completo del sync.
        var error = p.Get<string>("p_error_msg");
        if (!string.IsNullOrEmpty(error))
            logger.LogWarning("MergeLicitaciones: algunos items del lote fallaron: {Error}", error);

        return (p.Get<int>("p_creados"), p.Get<int>("p_actualizados"));
    }
}
