using Dapper;
using MPM.Core.Data;
using MPM.Modules.Mensajeria.Models;
using System.Data;
using System.Text.Json;

namespace MPM.Modules.Mensajeria.Data;

public class MensajeHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<PaginatedResult<MensajeDetalleDto>> ListarAsync(
        long conversacionId,
        string userId,
        int page,
        int pageSize,
        long? before,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new
        {
            p_conversacion_id = conversacionId,
            p_user_id = userId,
            p_page = page,
            p_page_size = pageSize,
            p_before = before
        };

        var result = await conn.QueryAsync<MensajeListResult>(
            sql: MensajeriaStoredProcedures.ListarMensajes,
            param: parameters,
            commandType: CommandType.Text);

        var items = result.Select(r => new MensajeDetalleDto
        {
            Id = r.Id,
            UserId = r.UserId,
            UserName = r.UserName,
            Tipo = r.Tipo,
            Contenido = r.Contenido,
            ReplyTo = r.ReplyToId.HasValue ? new MensajeResumenDto
            {
                Id = r.ReplyToId.Value,
                UserId = string.Empty,
                Tipo = "texto",
                Contenido = r.ReplyToContenido ?? string.Empty,
                CreatedAt = DateTime.MinValue
            } : null,
            Adjuntos = JsonSerializer.Deserialize<List<AdjuntoItemDto>>(r.Adjuntos ?? "[]", JsonDefaults.CamelCaseInsensitive) ?? new(),
            Estados = JsonSerializer.Deserialize<List<MensajeEstadoDto>>(r.Estados ?? "[]", JsonDefaults.CamelCaseInsensitive) ?? new(),
            EditedAt = r.EditedAt,
            CreatedAt = r.CreatedAt
        }).ToList();

        var totalCount = result.FirstOrDefault()?.TotalCount ?? 0;

        return new PaginatedResult<MensajeDetalleDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = (int)totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<(long Id, string? Error)> EnviarAsync(
        long conversacionId,
        string userId,
        string tipo,
        string? contenido,
        long? replyToId,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new DynamicParameters();
        parameters.Add("p_conversacion_id", conversacionId);
        parameters.Add("p_user_id", userId);
        parameters.Add("p_tipo", tipo);
        parameters.Add("p_contenido", contenido);
        parameters.Add("p_reply_to_id", replyToId);
        parameters.Add("p_id", dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        parameters.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.EnviarMensaje,
            param: parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string>("p_error_msg");
        var id = parameters.Get<long>("p_id");

        return (id, string.IsNullOrEmpty(errorMsg) ? null : errorMsg);
    }

    public async Task<string?> EditarAsync(long id, string userId, string contenido, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new DynamicParameters();
        parameters.Add("p_id", id);
        parameters.Add("p_user_id", userId);
        parameters.Add("p_contenido", contenido);
        parameters.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.EditarMensaje,
            param: parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string>("p_error_msg");
        return string.IsNullOrEmpty(errorMsg) ? null : errorMsg;
    }

    public async Task<string?> EliminarAsync(long id, string userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new DynamicParameters();
        parameters.Add("p_id", id);
        parameters.Add("p_user_id", userId);
        parameters.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.EliminarMensaje,
            param: parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string>("p_error_msg");
        return string.IsNullOrEmpty(errorMsg) ? null : errorMsg;
    }

    public async Task MarcarLeidoAsync(long mensajeId, string userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new { p_mensaje_id = mensajeId, p_user_id = userId };

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.MarcarLeido,
            param: parameters,
            commandType: CommandType.Text);
    }

    private class MensajeListResult
    {
        public long Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string? Contenido { get; set; }
        public long? ReplyToId { get; set; }
        public string? ReplyToContenido { get; set; }
        public string? Adjuntos { get; set; }
        public string? Estados { get; set; }
        public DateTime? EditedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public long TotalCount { get; set; }
    }
}
