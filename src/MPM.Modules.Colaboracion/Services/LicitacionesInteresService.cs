using MPM.Modules.Colaboracion.Data;
using MPM.Modules.Colaboracion.Models;

namespace MPM.Modules.Colaboracion.Services;

public class LicitacionesInteresService(LicitacionesInteresHandler handler)
{
    public virtual async Task<LicitacionInteresDto> MarcarInteresAsync(long licitacionId, string marcadoPor, CancellationToken ct = default)
    {
        return await handler.MarcarAsync(licitacionId, marcadoPor, ct);
    }

    public virtual async Task<LicitacionInteresDto?> ObtenerAsync(long licitacionId, CancellationToken ct = default)
    {
        return await handler.ObtenerPorLicitacionAsync(licitacionId, ct);
    }

    public virtual async Task<LicitacionInteresDto?> VincularAsync(long licitacionId, VincularInteresRequest request, CancellationToken ct = default)
    {
        if (request.WorkspaceId is { } workspaceId)
            await handler.VincularWorkspaceAsync(licitacionId, workspaceId, ct);

        if (request.ConversacionId is { } conversacionId)
            await handler.VincularConversacionAsync(licitacionId, conversacionId, ct);

        return await handler.ObtenerPorLicitacionAsync(licitacionId, ct);
    }

    public virtual async Task<List<LicitacionInteresListItemDto>> ListarAsync(CancellationToken ct = default)
    {
        return await handler.ListarAsync(ct);
    }
}
