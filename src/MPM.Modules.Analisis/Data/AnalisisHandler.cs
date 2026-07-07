using Dapper;
using MPM.Core.Data;
using MPM.Modules.Analisis.Models;
using System.Data;

namespace MPM.Modules.Analisis.Data;

public class AnalisisHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<(long Id, string? Error)> CrearWorkspaceAsync(long? licitacionId, string nombre, string userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_licitacion_id", licitacionId);
        p.Add("p_nombre", nombre);
        p.Add("p_user_id", userId);
        p.Add("p_id", dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        p.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        return await ExecuteReturningIdAsync(conn, AnalisisStoredProcedures.WorkspacesCrear, p, "p_id", ct);
    }

    public async Task<(List<WorkspaceItemDto> Items, long TotalCount)> ListarWorkspacesAsync(int page, int pageSize, string? search, string? estado, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var items = await conn.QueryAsync<WorkspaceItemDto>(
            AnalisisStoredProcedures.WorkspacesListar,
            new { p_page = page, p_page_size = pageSize, p_search = search, p_estado = estado },
            commandType: CommandType.Text);
        var list = items.ToList();
        var totalCount = list.Count > 0 ? list.First().TotalCount ?? 0 : 0;
        foreach (var item in list)
            item.TotalCount = null;
        return (list, totalCount);
    }

    public async Task<WorkspaceDetalleDto?> ObtenerWorkspaceAsync(long id, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QuerySingleOrDefaultAsync<WorkspaceDetalleDto>(
            AnalisisStoredProcedures.WorkspacesObtener,
            new { p_id = id },
            commandType: CommandType.Text);
    }

    public async Task<string?> ActualizarEstadoAsync(long id, string estado, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_id", id);
        p.Add("p_estado", estado);
        p.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(AnalisisStoredProcedures.WorkspacesActualizarEstado, p, commandType: CommandType.Text);
        return p.Get<string?>("p_error_msg");
    }

    public async Task<string?> EliminarWorkspaceAsync(long id, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_id", id);
        p.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(AnalisisStoredProcedures.WorkspacesEliminar, p, commandType: CommandType.Text);
        return p.Get<string?>("p_error_msg");
    }

    public async Task<(long Id, string? Error)> CrearDocumentoAsync(long workspaceId, string nombreArchivo, string mimeType, long tamanioBytes, string rutaStorage, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_workspace_id", workspaceId);
        p.Add("p_nombre_archivo", nombreArchivo);
        p.Add("p_mime_type", mimeType);
        p.Add("p_tamanio_bytes", tamanioBytes);
        p.Add("p_ruta_storage", rutaStorage);
        p.Add("p_id", dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        p.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        return await ExecuteReturningIdAsync(conn, AnalisisStoredProcedures.DocumentosCrear, p, "p_id", ct);
    }

    public async Task<IEnumerable<DocumentoItemDto>> ListarDocumentosAsync(long workspaceId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<DocumentoItemDto>(
            AnalisisStoredProcedures.DocumentosListar,
            new { p_workspace_id = workspaceId },
            commandType: CommandType.Text);
    }

    public async Task<DocumentoDetalleDto?> ObtenerDocumentoAsync(long id, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QuerySingleOrDefaultAsync<DocumentoDetalleDto>(
            AnalisisStoredProcedures.DocumentosObtener,
            new { p_id = id },
            commandType: CommandType.Text);
    }

    public async Task<(long Id, string? Error)> CrearResultadoAsync(long workspaceId, long documentoId, string contenidoJson, string modeloUsado, int tokensEntrada, int tokensSalida, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_workspace_id", workspaceId);
        p.Add("p_documento_id", documentoId);
        p.Add("p_contenido_json", contenidoJson);
        p.Add("p_modelo_usado", modeloUsado);
        p.Add("p_tokens_entrada", tokensEntrada);
        p.Add("p_tokens_salida", tokensSalida);
        p.Add("p_id", dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        p.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        return await ExecuteReturningIdAsync(conn, AnalisisStoredProcedures.ResultadosCrear, p, "p_id", ct);
    }

    public async Task<ResultadoDto?> ObtenerResultadoPorWorkspaceAsync(long workspaceId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QuerySingleOrDefaultAsync<ResultadoDto>(
            AnalisisStoredProcedures.ResultadosObtenerPorWorkspace,
            new { p_workspace_id = workspaceId },
            commandType: CommandType.Text);
    }

    public async Task<ResultadoDto?> ObtenerResultadoPorLicitacionAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QuerySingleOrDefaultAsync<ResultadoDto>(
            AnalisisStoredProcedures.ResultadosObtenerPorLicitacion,
            new { p_licitacion_id = licitacionId },
            commandType: CommandType.Text);
    }

    public async Task<(long Id, string? Error)> ObtenerOCrearChatConversacionAsync(long workspaceId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_workspace_id", workspaceId);
        p.Add("p_conversacion_id", dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        p.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        return await ExecuteReturningIdAsync(conn, AnalisisStoredProcedures.ChatObtenerOCrearConversacion, p, "p_conversacion_id", ct);
    }

    public async Task<(long Id, string? Error)> CrearMensajeChatAsync(long conversacionId, string rol, string contenido, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var p = new DynamicParameters();
        p.Add("p_conversacion_id", conversacionId);
        p.Add("p_rol", rol);
        p.Add("p_contenido", contenido);
        p.Add("p_id", dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        p.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        return await ExecuteReturningIdAsync(conn, AnalisisStoredProcedures.ChatEnviarMensaje, p, "p_id", ct);
    }

    public async Task<IEnumerable<ChatMensajeDto>> ObtenerHistorialChatAsync(long conversacionId, int limit = 50, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<ChatMensajeDto>(
            AnalisisStoredProcedures.ChatObtenerHistorial,
            new { p_conversacion_id = conversacionId, p_limit = limit },
            commandType: CommandType.Text);
    }

    public async Task<IEnumerable<ResultadoCompletoDto>> ObtenerResultadosCompletosAsync(int? anio, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return await conn.QueryAsync<ResultadoCompletoDto>(
            AnalisisStoredProcedures.ResultadosObtenerCompletos,
            new { p_anio = anio },
            commandType: CommandType.Text);
    }

    private static async Task<(long Id, string? Error)> ExecuteReturningIdAsync(
        System.Data.Common.DbConnection conn,
        string sql,
        DynamicParameters parameters,
        string idParamName,
        CancellationToken ct = default)
    {
        await conn.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
        var error = parameters.Get<string?>("p_error_msg");
        if (!string.IsNullOrEmpty(error)) return (0, error);
        var id = parameters.Get<long?>(idParamName) ?? 0;
        return (id, null);
    }
}
