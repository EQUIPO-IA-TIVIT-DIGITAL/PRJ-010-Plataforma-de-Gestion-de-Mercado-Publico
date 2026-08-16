using System.Data;
using Dapper;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Models;

namespace MPM.Modules.Licitaciones.Data;

public class AdjuntoDocumentosHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public virtual async Task<List<AdjuntoDocumentoFila>> ListarAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var rows = await conn.QueryAsync<AdjuntoDocumentoFila>(
            AdjuntoDocumentosStoredProcedures.ListarPorLicitacion,
            new { p_licitacion_id = licitacionId },
            commandType: CommandType.Text);
        return rows.ToList();
    }

    public virtual async Task<string?> MarcarDescargaIniciadaAsync(long licitacionId, string iniciadaPor, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<ErrorResult>(
            AdjuntoDocumentosStoredProcedures.MarcarDescargaIniciada,
            new { p_licitacion_id = licitacionId, p_iniciada_por = iniciadaPor, p_error_msg = "" },
            commandType: CommandType.Text);
        return result.FirstOrDefault()?.p_error_msg is { Length: > 0 } err ? err : null;
    }

    public virtual async Task<string?> MarcarDescargaFinalizadaAsync(long licitacionId, string estado, string? error, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<ErrorResult>(
            AdjuntoDocumentosStoredProcedures.MarcarDescargaFinalizada,
            new { p_licitacion_id = licitacionId, p_estado = estado, p_error = error, p_error_msg = "" },
            commandType: CommandType.Text);
        return result.FirstOrDefault()?.p_error_msg is { Length: > 0 } err ? err : null;
    }

    public virtual async Task<bool> ExistenDescargasVivasAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<int>(
            AdjuntoDocumentosStoredProcedures.ExistenDescargasVivas,
            new { p_licitacion_id = licitacionId },
            commandType: CommandType.Text);
        return result.FirstOrDefault() > 0;
    }

    public class AdjuntoDocumentoFila
    {
        public long Id { get; set; }
        public long LicitacionId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string NombreArchivo { get; set; } = string.Empty;
        public string? NombreElemento { get; set; }
        public string RutaStorage { get; set; } = string.Empty;
        public string? RutaLocal { get; set; }
        public long? TamanioBytes { get; set; }
        public string? MimeType { get; set; }
        public bool ActaDescargada { get; set; }
        public string? Sha256Hash { get; set; }
        public string? FechaGrilla { get; set; }
        public int Version { get; set; }
        public string DescargaEstado { get; set; } = "pendiente";
        public string? DescargaError { get; set; }
        public string? DescargaIniciadaPor { get; set; }
        public DateTime? DescargaIniciadaAt { get; set; }
        public DateTime? DescargaFinAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private class ErrorResult
    {
        public string? p_error_msg { get; set; }
    }
}
