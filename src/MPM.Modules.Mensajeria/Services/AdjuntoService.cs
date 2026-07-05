using MPM.Modules.Mensajeria.Data;
using MPM.Modules.Mensajeria.Models;

namespace MPM.Modules.Mensajeria.Services;

public class AdjuntoService(AdjuntoHandler handler)
{
    private readonly AdjuntoHandler _handler = handler;

    public async Task<(long Id, string? Error)> CrearAsync(
        long mensajeId,
        string nombreArchivo,
        string mimeType,
        long tamanioBytes,
        string rutaStorage,
        CancellationToken ct = default)
    {
        return await _handler.CrearAsync(mensajeId, nombreArchivo, mimeType, tamanioBytes, rutaStorage, ct);
    }

    public async Task<AdjuntoDetalleDto?> ObtenerAsync(long id, long conversacionId, string userId, CancellationToken ct = default)
    {
        return await _handler.ObtenerAsync(id, conversacionId, userId, ct);
    }
}
