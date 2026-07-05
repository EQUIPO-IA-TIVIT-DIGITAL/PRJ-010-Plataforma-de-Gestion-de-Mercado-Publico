using Dapper;
using MPM.Core.Data;
using MPM.Modules.Mensajeria.Models;
using System.Data;

namespace MPM.Modules.Mensajeria.Data;

public class AdjuntoHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public async Task<(long Id, string? Error)> CrearAsync(
        long mensajeId,
        string nombreArchivo,
        string mimeType,
        long tamanioBytes,
        string rutaStorage,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new DynamicParameters();
        parameters.Add("p_mensaje_id", mensajeId);
        parameters.Add("p_nombre_archivo", nombreArchivo);
        parameters.Add("p_mime_type", mimeType);
        parameters.Add("p_tamanio_bytes", tamanioBytes);
        parameters.Add("p_ruta_storage", rutaStorage);
        parameters.Add("p_id", dbType: DbType.Int64, direction: ParameterDirection.InputOutput);
        parameters.Add("p_error_msg", dbType: DbType.String, size: 1000, direction: ParameterDirection.InputOutput);

        await conn.ExecuteAsync(
            sql: MensajeriaStoredProcedures.CrearAdjunto,
            param: parameters,
            commandType: CommandType.Text);

        var errorMsg = parameters.Get<string>("p_error_msg");
        var id = parameters.Get<long>("p_id");

        return (id, string.IsNullOrEmpty(errorMsg) ? null : errorMsg);
    }

    public async Task<AdjuntoDetalleDto?> ObtenerAsync(long id, long conversacionId, string userId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        
        var parameters = new
        {
            p_id = id,
            p_conversacion_id = conversacionId,
            p_user_id = userId
        };

        var result = await conn.QueryFirstOrDefaultAsync<AdjuntoDetalleResult>(
            sql: MensajeriaStoredProcedures.ObtenerAdjunto,
            param: parameters,
            commandType: CommandType.Text);

        if (result == null)
            return null;

        return new AdjuntoDetalleDto
        {
            Id = result.Id,
            MensajeId = result.MensajeId,
            NombreArchivo = result.NombreArchivo,
            MimeType = result.MimeType,
            TamanioBytes = result.TamanioBytes,
            RutaStorage = result.RutaStorage,
            CreatedAt = result.CreatedAt
        };
    }

    private class AdjuntoDetalleResult
    {
        public long Id { get; set; }
        public long MensajeId { get; set; }
        public string NombreArchivo { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long TamanioBytes { get; set; }
        public string RutaStorage { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
