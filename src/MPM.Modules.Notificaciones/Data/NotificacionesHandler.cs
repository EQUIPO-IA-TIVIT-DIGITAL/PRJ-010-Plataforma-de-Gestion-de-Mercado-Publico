using Dapper;
using MPM.Core.Data;
using MPM.Modules.Notificaciones.Models;
using System.Data;

namespace MPM.Modules.Notificaciones.Data;

public class NotificacionesHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<(long Id, string? Error)> CrearAsync(
        string usuarioId, string tipo, string titulo, string mensaje, string? metadataJson,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        var result = await conn.QueryAsync<CrearResult>(
            NotificacionesStoredProcedures.Crear,
            new { p_usuario_id = usuarioId, p_tipo = tipo, p_titulo = titulo, p_mensaje = mensaje, p_metadata = metadataJson },
            commandType: CommandType.Text);

        var row = result.FirstOrDefault();
        if (row == null)
            return (0, "SYS_001: Sin respuesta del servidor");

        var error = string.IsNullOrEmpty(row.p_error_msg) ? null : row.p_error_msg;
        return (row.p_id, error);
    }

    public async Task<(List<NotificacionItemDto> Items, long TotalCount)> ListarAsync(
        string usuarioId, int page, int pageSize, bool soloNoLeidas = false,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        var result = await conn.QueryAsync<NotificacionItemDto>(
            NotificacionesStoredProcedures.Listar,
            new
            {
                p_usuario_id = usuarioId,
                p_page = page,
                p_page_size = pageSize,
                p_solo_no_leidas = soloNoLeidas,
            },
            commandType: CommandType.Text);

        var items = result.ToList();
        var totalCount = items.Count > 0 ? items.First().TotalCount ?? 0 : 0;

        foreach (var item in items)
        {
            item.TotalCount = null;
            // notificaciones.created_at es TIMESTAMP sin zona horaria (poblado con
            // CURRENT_TIMESTAMP del servidor Postgres, en UTC); Npgsql lo mapea con
            // Kind=Unspecified y System.Text.Json lo serializa sin offset, lo que el
            // navegador interpreta como hora local y desfasa la notificación (ver
            // specs/030-qol-frontend-y-fix-scraper/research.md §2). Se marca como UTC
            // explícito para que el frontend pueda convertirlo correctamente.
            item.CreatedAt = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc);
        }

        return (items, totalCount);
    }

    public async Task<long> ContarNoLeidasAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        var result = await conn.QueryAsync<NotificacionesCountDto>(
            NotificacionesStoredProcedures.ContarNoLeidas,
            new { p_usuario_id = usuarioId },
            commandType: CommandType.Text);

        return result.FirstOrDefault()?.Count ?? 0;
    }

    public async Task<string?> MarcarLeidaAsync(long id, string usuarioId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        var result = await conn.QueryAsync<ErrorResult>(
            NotificacionesStoredProcedures.MarcarLeida,
            new { p_id = id, p_usuario_id = usuarioId },
            commandType: CommandType.Text);

        var error = result.FirstOrDefault()?.p_error_msg;
        return string.IsNullOrEmpty(error) ? null : error;
    }

    public async Task<(int Count, string? Error)> MarcarTodasLeidasAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        var result = await conn.QueryAsync<MarcarTodasResult>(
            NotificacionesStoredProcedures.MarcarTodasLeidas,
            new { p_usuario_id = usuarioId },
            commandType: CommandType.Text);

        var row = result.FirstOrDefault();
        if (row == null)
            return (0, "SYS_001: Sin respuesta del servidor");

        var error = string.IsNullOrEmpty(row.p_error_msg) ? null : row.p_error_msg;
        return (row.p_count, error);
    }

    public async Task<string?> EliminarAsync(long id, string usuarioId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        var result = await conn.QueryAsync<ErrorResult>(
            NotificacionesStoredProcedures.Eliminar,
            new { p_id = id, p_usuario_id = usuarioId },
            commandType: CommandType.Text);

        var error = result.FirstOrDefault()?.p_error_msg;
        return string.IsNullOrEmpty(error) ? null : error;
    }

    public async Task<(int Count, string? Error)> EliminarTodasAsync(string usuarioId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();

        var result = await conn.QueryAsync<MarcarTodasResult>(
            NotificacionesStoredProcedures.EliminarTodas,
            new { p_usuario_id = usuarioId },
            commandType: CommandType.Text);

        var row = result.FirstOrDefault();
        if (row == null)
            return (0, "SYS_001: Sin respuesta del servidor");

        var error = string.IsNullOrEmpty(row.p_error_msg) ? null : row.p_error_msg;
        return (row.p_count, error);
    }

    private class CrearResult
    {
        public long p_id { get; set; }
        public string? p_error_msg { get; set; }
    }

    private class ErrorResult
    {
        public string? p_error_msg { get; set; }
    }

    private class MarcarTodasResult
    {
        public int p_count { get; set; }
        public string? p_error_msg { get; set; }
    }
}
