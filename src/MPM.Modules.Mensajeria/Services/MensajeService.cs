using MPM.Modules.Mensajeria.Data;
using MPM.Modules.Mensajeria.Models;

namespace MPM.Modules.Mensajeria.Services;

public class MensajeService(MensajeHandler handler)
{
    private readonly MensajeHandler _handler = handler;

    public async Task<PaginatedResult<MensajeDetalleDto>> ListarAsync(
        long conversacionId,
        string userId,
        int page,
        int pageSize,
        long? before,
        CancellationToken ct = default)
    {
        return await _handler.ListarAsync(conversacionId, userId, page, pageSize, before, ct);
    }

    public async Task<(long Id, string? Error)> EnviarAsync(
        long conversacionId,
        string userId,
        string tipo,
        string? contenido,
        long? replyToId,
        CancellationToken ct = default)
    {
        return await _handler.EnviarAsync(conversacionId, userId, tipo, contenido, replyToId, ct);
    }

    public async Task<string?> EditarAsync(long id, string userId, string contenido, CancellationToken ct = default)
    {
        return await _handler.EditarAsync(id, userId, contenido, ct);
    }

    public async Task<string?> EliminarAsync(long id, string userId, CancellationToken ct = default)
    {
        return await _handler.EliminarAsync(id, userId, ct);
    }

    public async Task MarcarLeidoAsync(long mensajeId, string userId, CancellationToken ct = default)
    {
        await _handler.MarcarLeidoAsync(mensajeId, userId, ct);
    }
}
