using System.Data;
using System.Text.Json;
using Dapper;
using MPM.Core.Data;

namespace MPM.Modules.Colaboracion.Data;

/// <summary>
/// Acceso a datos de la decisión GO/NO GO (V144): registra sobre licitaciones_interes
/// (crea la fila de interés si no existe, capturando el estado real de la licitación) y
/// obtiene el estado vigente. El snapshot IA se lee de analisis_licitacion_comercial (V142).
/// </summary>
public class DecisionHandler(DbConnectionFactory dbFactory)
{
    private readonly DbConnectionFactory _dbFactory = dbFactory;

    public virtual async Task RegistrarAsync(
        long licitacionId, string decision, string? motivo, string? recomendacionIa,
        decimal? scoreConfianza, string decididoPor, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var result = await conn.QueryAsync<RegistrarResult>(
            DecisionStoredProcedures.Registrar,
            new
            {
                p_licitacion_id = licitacionId,
                p_decision = decision,
                p_motivo = motivo,
                p_recomendacion_ia = recomendacionIa,
                p_score_confianza = scoreConfianza,
                p_decidido_por = decididoPor,
                p_id = 0L,
                p_error_msg = "",
            },
            commandType: CommandType.Text);

        var fila = result.FirstOrDefault();
        if (fila?.p_error_msg is { Length: > 0 } err)
            throw new InvalidOperationException($"No se pudo registrar la decisión: {err}");
    }

    public virtual async Task<DecisionRow?> ObtenerAsync(long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        return (await conn.QueryAsync<DecisionRow>(
            DecisionStoredProcedures.Obtener,
            new { p_licitacion_id = licitacionId },
            commandType: CommandType.Text)).FirstOrDefault();
    }

    /// <summary>
    /// Snapshot IA (DEC-R005/R006): recomendación y score del último análisis comercial
    /// completado; sin análisis completado → (null, null) — decisión 100 % humana permitida.
    /// </summary>
    public virtual async Task<(string? GoNoGo, decimal? ScoreConfianza)> RecomendacionAnalisisAsync(
        long licitacionId, CancellationToken ct = default)
    {
        await using var conn = _dbFactory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<RecomendacionRow>(
            "SELECT go_no_go, score_confianza FROM analisis_licitacion_comercial " +
            "WHERE licitacion_id = @p_licitacion_id AND estado = 'completado' " +
            "ORDER BY id DESC LIMIT 1",
            new { p_licitacion_id = licitacionId });

        return (row?.GoNoGo, row?.ScoreConfianza);
    }

    /// <summary>Fila de la decisión vigente (mapeo de usp_LicitacionesDecision_Obtener).</summary>
    public class DecisionRow
    {
        public long Id { get; set; }
        public long LicitacionId { get; set; }
        public string? Decision { get; set; }
        public string? Motivo { get; set; }
        public string? RecomendacionIa { get; set; }
        public decimal? ScoreConfianza { get; set; }
        public string? DecididoPor { get; set; }
        public DateTime? DecididoAt { get; set; }
        public string? Notificados { get; set; } // JSONB → string (Fase 3 lo estructura)
        public DateTime? NotificadoAt { get; set; }
    }

    private class RegistrarResult
    {
        public long p_id { get; set; }
        public string? p_error_msg { get; set; }
    }

    private class RecomendacionRow
    {
        public string? GoNoGo { get; set; }
        public decimal? ScoreConfianza { get; set; }
    }
}
