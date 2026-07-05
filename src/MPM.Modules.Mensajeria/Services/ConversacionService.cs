using MPM.Modules.Mensajeria.Data;
using MPM.Modules.Mensajeria.Models;

namespace MPM.Modules.Mensajeria.Services;

public class ConversacionService(ConversacionHandler handler)
{
    private readonly ConversacionHandler _handler = handler;

    public async Task<PaginatedResult<ConversacionResumenDto>> ListarAsync(
        string userId,
        int page,
        int pageSize,
        string? search,
        string sortBy,
        string sortDir,
        CancellationToken ct = default)
    {
        return await _handler.ListarAsync(userId, page, pageSize, search, sortBy, sortDir, ct);
    }

    public async Task<ConversacionDetalleDto?> ObtenerAsync(long id, string userId, CancellationToken ct = default)
    {
        return await _handler.ObtenerAsync(id, userId, ct);
    }

    public async Task<(long Id, string? Error)> CrearAsync(
        string tipo,
        string? asunto,
        long? licitacionId,
        List<string> participanteIds,
        string creadorId,
        CancellationToken ct = default)
    {
        return await _handler.CrearAsync(tipo, asunto, licitacionId, participanteIds, creadorId, ct);
    }

    public async Task<string?> ActualizarAsync(long id, string asunto, string userId, CancellationToken ct = default)
    {
        return await _handler.ActualizarAsync(id, asunto, userId, ct);
    }

    public async Task<string?> AbandonarAsync(long id, string userId, CancellationToken ct = default)
    {
        return await _handler.AbandonarAsync(id, userId, ct);
    }

    public async Task<string?> AgregarParticipanteAsync(
        long conversacionId,
        string userId,
        string rol,
        string solicitanteId,
        CancellationToken ct = default)
    {
        return await _handler.AgregarParticipanteAsync(conversacionId, userId, rol, solicitanteId, ct);
    }

    public async Task<string?> QuitarParticipanteAsync(
        long conversacionId,
        string userId,
        string solicitanteId,
        CancellationToken ct = default)
    {
        return await _handler.QuitarParticipanteAsync(conversacionId, userId, solicitanteId, ct);
    }
}
