using Dapper;
using MPM.Core.Data;
using MPM.Modules.Mensajeria.Models;
using System.Data;
using System.Text.Json;

namespace MPM.Modules.Mensajeria.Data;

public class PresenciaHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task ActualizarAsync(string userId, string estado, long? conversacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new
        {
            p_user_id = userId,
            p_estado = estado,
            p_conversacion_id = conversacionId
        };

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.ActualizarPresencia,
            param: parameters,
            commandType: CommandType.Text);
    }

    public async Task<List<PresenciaDto>> ObtenerAsync(List<string> userIds, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new
        {
            p_user_ids = JsonSerializer.Serialize(userIds)
        };

        var result = await conn.QueryAsync<PresenciaResult>(
            sql: MensajeriaStoredProcedures.ObtenerPresencia,
            param: parameters,
            commandType: CommandType.Text);

        return result.Select(r => new PresenciaDto
        {
            UserId = r.UserId,
            Estado = r.Estado,
            UpdatedAt = r.UpdatedAt
        }).ToList();
    }

    private class PresenciaResult
    {
        public string UserId { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
    }
}
