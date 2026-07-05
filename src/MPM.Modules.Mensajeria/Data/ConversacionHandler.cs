using Dapper;
using MPM.Core.Data;
using MPM.Modules.Mensajeria.Models;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MPM.Modules.Mensajeria.Data;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions CamelCaseInsensitive = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}

public class ConversacionHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<PaginatedResult<ConversacionResumenDto>> ListarAsync(
        string userId,
        int page,
        int pageSize,
        string? search,
        string sortBy,
        string sortDir,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new
        {
            p_user_id = userId,
            p_page = page,
            p_page_size = pageSize,
            p_search = search,
            p_sort_by = sortBy,
            p_sort_dir = sortDir
        };

        var result = await conn.QueryAsync<ConversacionListResult>(
            sql: MensajeriaStoredProcedures.ListarConversaciones,
            param: parameters,
            commandType: CommandType.Text);

        var items = result.Select(r => new ConversacionResumenDto
        {
            Id = r.Id,
            Tipo = r.Tipo,
            Asunto = r.Asunto,
            LicitacionId = r.LicitacionId,
            LicitacionNombre = r.LicitacionNombre,
            Participantes = JsonSerializer.Deserialize<List<ParticipanteItemDto>>(r.Participantes ?? "[]", JsonDefaults.CamelCaseInsensitive) ?? new(),
            UltimoMensaje = string.IsNullOrEmpty(r.UltimoMensaje) 
                ? null 
                : JsonSerializer.Deserialize<MensajeResumenDto>(r.UltimoMensaje, JsonDefaults.CamelCaseInsensitive),
            NoLeidos = r.NoLeidos,
            UpdatedAt = r.UpdatedAt
        }).ToList();

        var totalCount = result.FirstOrDefault()?.TotalCount ?? 0;

        return new PaginatedResult<ConversacionResumenDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalRecords = (int)totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<ConversacionDetalleDto?> ObtenerAsync(long id, string userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new { p_id = id, p_user_id = userId };

        var result = await conn.QueryFirstOrDefaultAsync<ConversacionDetalleResult>(
            sql: MensajeriaStoredProcedures.ObtenerConversacion,
            param: parameters,
            commandType: CommandType.Text);

        if (result == null)
            return null;

        return new ConversacionDetalleDto
        {
            Id = result.Id,
            Tipo = result.Tipo,
            Asunto = result.Asunto,
            LicitacionId = result.LicitacionId,
            LicitacionNombre = result.LicitacionNombre,
            Participantes = JsonSerializer.Deserialize<List<ParticipanteItemDto>>(result.Participantes ?? "[]", JsonDefaults.CamelCaseInsensitive) ?? new(),
            CreatedAt = result.CreatedAt,
            UpdatedAt = result.UpdatedAt
        };
    }

    public async Task<(long Id, string? Error)> CrearAsync(
        string tipo,
        string? asunto,
        long? licitacionId,
        List<string> participanteIds,
        string creadorId,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new DynamicParameters();
        parameters.Add("p_tipo", tipo);
        parameters.Add("p_asunto", asunto);
        parameters.Add("p_licitacion_id", licitacionId);
        parameters.Add("p_participante_ids", JsonSerializer.Serialize(participanteIds));
        parameters.Add("p_creador_id", creadorId);
        parameters.Add("p_id", dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        parameters.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.CrearConversacion,
            param: parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string>("p_error_msg");
        var id = parameters.Get<long?>("p_id") ?? 0;

        return (id, string.IsNullOrEmpty(errorMsg) ? null : errorMsg);
    }

    public async Task<string?> ActualizarAsync(long id, string asunto, string userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new DynamicParameters();
        parameters.Add("p_id", id);
        parameters.Add("p_asunto", asunto);
        parameters.Add("p_user_id", userId);
        parameters.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.ActualizarConversacion,
            param: parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string>("p_error_msg");
        return string.IsNullOrEmpty(errorMsg) ? null : errorMsg;
    }

    public async Task<string?> AbandonarAsync(long id, string userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new DynamicParameters();
        parameters.Add("p_id", id);
        parameters.Add("p_user_id", userId);
        parameters.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.AbandonarConversacion,
            param: parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string>("p_error_msg");
        return string.IsNullOrEmpty(errorMsg) ? null : errorMsg;
    }

    public async Task<string?> AgregarParticipanteAsync(
        long conversacionId,
        string userId,
        string rol,
        string solicitanteId,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new DynamicParameters();
        parameters.Add("p_conversacion_id", conversacionId);
        parameters.Add("p_user_id", userId);
        parameters.Add("p_rol", rol);
        parameters.Add("p_solicitante_id", solicitanteId);
        parameters.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.AgregarParticipante,
            param: parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string>("p_error_msg");
        return string.IsNullOrEmpty(errorMsg) ? null : errorMsg;
    }

    public async Task<string?> QuitarParticipanteAsync(
        long conversacionId,
        string userId,
        string solicitanteId,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new DynamicParameters();
        parameters.Add("p_conversacion_id", conversacionId);
        parameters.Add("p_user_id", userId);
        parameters.Add("p_solicitante_id", solicitanteId);
        parameters.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.QuitarParticipante,
            param: parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string>("p_error_msg");
        return string.IsNullOrEmpty(errorMsg) ? null : errorMsg;
    }

    private class ConversacionListResult
    {
        public long Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string? Asunto { get; set; }
        public long? LicitacionId { get; set; }
        public string? LicitacionNombre { get; set; }
        public string? Participantes { get; set; }
        public string? UltimoMensaje { get; set; }
        public long NoLeidos { get; set; }
        public DateTime UpdatedAt { get; set; }
        public long TotalCount { get; set; }
    }

    private class ConversacionDetalleResult
    {
        public long Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string? Asunto { get; set; }
        public long? LicitacionId { get; set; }
        public string? LicitacionNombre { get; set; }
        public string? Participantes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
