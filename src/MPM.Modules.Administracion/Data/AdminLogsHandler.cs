using Dapper;
using MPM.Core.Data;
using MPM.Modules.Administracion.Models;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace MPM.Modules.Administracion.Data;

/// <summary>
/// Lectura unificada de logs/auditoría vía usp_Admin_ListarLogs (V132).
/// Los 5 orígenes (auth, sync, scraper, extraccion, ai_provider) se normalizan
/// en la BD a filas homogéneas con detalle legible + payload JSON crudo.
/// </summary>
public class AdminLogsHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<IEnumerable<AdminLogItemDto>> ListarLogsAsync(
        string? tipo, DateTime? desde, DateTime? hasta, string? estado, int limite,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<AdminLogItemDto>(
            sql: "SELECT * FROM usp_Admin_ListarLogs(@p_tipo, @p_desde, @p_hasta, @p_estado, @p_limite)",
            param: new
            {
                p_tipo = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim().ToLowerInvariant(),
                p_desde = desde,
                p_hasta = hasta,
                p_estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToLowerInvariant(),
                p_limite = limite,
            },
            commandType: CommandType.Text);
    }
}
