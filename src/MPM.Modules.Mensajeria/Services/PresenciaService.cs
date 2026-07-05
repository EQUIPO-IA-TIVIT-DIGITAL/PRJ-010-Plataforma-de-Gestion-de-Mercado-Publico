using MPM.Modules.Mensajeria.Data;
using MPM.Modules.Mensajeria.Models;

namespace MPM.Modules.Mensajeria.Services;

public class PresenciaService(PresenciaHandler handler)
{
    private readonly PresenciaHandler _handler = handler;

    public async Task ActualizarAsync(string userId, string estado, long? conversacionId, CancellationToken ct = default)
    {
        await _handler.ActualizarAsync(userId, estado, conversacionId, ct);
    }

    public async Task<List<PresenciaDto>> ObtenerAsync(List<string> userIds, CancellationToken ct = default)
    {
        return await _handler.ObtenerAsync(userIds, ct);
    }
}
