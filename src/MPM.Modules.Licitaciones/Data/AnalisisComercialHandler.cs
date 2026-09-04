using System.Data;
using Dapper;
using MPM.Core.Data;

namespace MPM.Modules.Licitaciones.Data;

public class AnalisisComercialHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public virtual async Task<AnalisisComercialFila?> ObtenerUltimoAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return (await conn.QueryAsync<AnalisisComercialFila>(
            AnalisisComercialStoredProcedures.ObtenerUltimo,
            new { p_licitacion_id = licitacionId },
            commandType: CommandType.Text)).FirstOrDefault();
    }

    public virtual async Task<(long Id, bool YaExistia, string? Error)> IniciarAsync(
        long licitacionId, string conjuntoHash, string creadoPor, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<IniciarResult>(
            AnalisisComercialStoredProcedures.Iniciar,
            new
            {
                p_licitacion_id = licitacionId,
                p_conjunto_hash = conjuntoHash,
                p_creado_por = creadoPor,
                p_id = 0L,
                p_ya_existia = false,
                p_error_msg = "",
            },
            commandType: CommandType.Text);

        var fila = result.FirstOrDefault();
        var error = fila?.p_error_msg is { Length: > 0 } err ? err : null;
        return (fila?.p_id ?? 0, fila?.p_ya_existia ?? false, error);
    }

    public virtual async Task<string?> CompletarAsync(
        long id, string estado, string? resultadoJson, string? resumen, string? goNoGo,
        decimal? score, string? modelo, int? tokensIn, int? tokensOut, string? error,
        CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<ErrorResult>(
            AnalisisComercialStoredProcedures.Completar,
            new
            {
                p_id = id,
                p_estado = estado,
                p_resultado_json = resultadoJson,
                p_resumen_ejecutivo = resumen,
                p_go_no_go = goNoGo,
                p_score_confianza = score,
                p_modelo_usado = modelo,
                p_tokens_entrada = tokensIn,
                p_tokens_salida = tokensOut,
                p_error = error,
                p_error_msg = "",
            },
            commandType: CommandType.Text);
        return result.FirstOrDefault()?.p_error_msg is { Length: > 0 } err ? err : null;
    }

    /// <summary>F1-T7 Go/No-Go por tipo: resuelve tipo oficial de la licitación (código + nombre catálogo). No lanza si falla — caller hace fallback genérico (GO-R013).</summary>
    public virtual async Task<(string? TipoCodigo, string? TipoNombre)> ObtenerTipoLicitacionAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<TipoLicitacionRow>(
            "SELECT l.tipo AS TipoCodigo, t.nombre AS TipoNombre FROM licitaciones l LEFT JOIN tipos_licitacion t ON t.codigo = l.tipo WHERE l.id = @licitacionId",
            new { licitacionId },
            commandType: CommandType.Text);
        return (row?.TipoCodigo, row?.TipoNombre);
    }

    private class TipoLicitacionRow
    {
        public string? TipoCodigo { get; set; }
        public string? TipoNombre { get; set; }
    }

    public class AnalisisComercialFila
    {
        public long Id { get; set; }
        public long LicitacionId { get; set; }
        public string ConjuntoHash { get; set; } = string.Empty;
        public string Estado { get; set; } = "pendiente";
        public string? ResultadoJson { get; set; }
        public string? ResumenEjecutivo { get; set; }
        public string? GoNoGo { get; set; }
        public decimal? ScoreConfianza { get; set; }
        public string? ModeloUsado { get; set; }
        public int? TokensEntrada { get; set; }
        public int? TokensSalida { get; set; }
        public string? Error { get; set; }
        public string? CreadoPor { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private class IniciarResult
    {
        public long p_id { get; set; }
        public bool p_ya_existia { get; set; }
        public string? p_error_msg { get; set; }
    }

    private class ErrorResult
    {
        public string? p_error_msg { get; set; }
    }
}
