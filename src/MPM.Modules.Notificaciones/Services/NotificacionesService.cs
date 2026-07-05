using MPM.Modules.Notificaciones.Data;
using MPM.Modules.Notificaciones.Models;
using System.Text.Json;

namespace MPM.Modules.Notificaciones.Services;

public class NotificacionesService(NotificacionesHandler handler)
{
    private readonly NotificacionesHandler _handler = handler;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<(long Id, string? Error)> CrearAsync(
        string usuarioId, string tipo, string titulo, string mensaje, object? metadata = null)
    {
        var metadataJson = metadata != null
            ? JsonSerializer.Serialize(metadata, _jsonOptions)
            : null;

        return await _handler.CrearAsync(usuarioId, tipo, titulo, mensaje, metadataJson);
    }

    public async Task<PaginatedResult<NotificacionItemDto>> ListarAsync(
        string usuarioId, int page = 1, int pageSize = 20, bool soloNoLeidas = false)
    {
        var (items, totalCount) = await _handler.ListarAsync(usuarioId, page, pageSize, soloNoLeidas);

        return new PaginatedResult<NotificacionItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalCount,
            TotalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0,
        };
    }

    public async Task<long> ContarNoLeidasAsync(string usuarioId)
    {
        return await _handler.ContarNoLeidasAsync(usuarioId);
    }

    public async Task<string?> MarcarLeidaAsync(long id, string usuarioId)
    {
        return await _handler.MarcarLeidaAsync(id, usuarioId);
    }

    public async Task<(int Count, string? Error)> MarcarTodasLeidasAsync(string usuarioId)
    {
        return await _handler.MarcarTodasLeidasAsync(usuarioId);
    }

    public async Task<string?> EliminarAsync(long id, string usuarioId)
    {
        return await _handler.EliminarAsync(id, usuarioId);
    }

    public async Task<(int Count, string? Error)> EliminarTodasAsync(string usuarioId)
    {
        return await _handler.EliminarTodasAsync(usuarioId);
    }
}
