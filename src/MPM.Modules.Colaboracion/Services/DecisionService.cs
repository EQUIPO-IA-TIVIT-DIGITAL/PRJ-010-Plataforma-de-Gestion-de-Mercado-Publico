using MPM.Modules.Colaboracion.Data;
using MPM.Modules.Colaboracion.Models;

namespace MPM.Modules.Colaboracion.Services;

/// <summary>
/// Decisión formal GO/NO GO (DEC-R001..R011): siempre humana, motivo obligatorio en NO GO,
/// snapshot de la recomendación IA copiado del último análisis completado al momento de
/// decidir (inmutable frente a re-análisis). 1 fila por licitación — re-decidir reemplaza.
/// </summary>
public class DecisionService(DecisionHandler handler)
{
    /// <summary>Decisión inválida (DEC_002 → 422) o body inválido (VAL_001 → 400).</summary>
    public class DecisionValidationException(string errorCode, string message) : Exception(message)
    {
        public string ErrorCode { get; } = errorCode;
    }

    public virtual async Task<DecisionDto> RegistrarAsync(
        long licitacionId, string codigoExterno, string decididoPor, DecisionRequest request,
        CancellationToken ct = default)
    {
        // DEC-R001: solo go/no_go (case-insensitive, normalizado a minúsculas).
        var decision = request.Decision?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(decision))
            throw new DecisionValidationException("VAL_001", "El campo 'decision' es obligatorio (go | no_go)");
        if (decision != "go" && decision != "no_go")
            throw new DecisionValidationException("DEC_002", "La decisión debe ser 'go' o 'no_go'");

        // DEC-R002: motivo obligatorio en NO GO (mín. 10 caracteres); DEC-R003: máx 4.000 en GO.
        var motivo = request.Motivo?.Trim();
        if (motivo != null && motivo.Length > 4000)
            throw new DecisionValidationException("VAL_001", "El motivo no puede superar los 4.000 caracteres");
        if (decision == "no_go" && (string.IsNullOrWhiteSpace(motivo) || motivo!.Length < 10))
            throw new DecisionValidationException("DEC_002", "El motivo es obligatorio en NO GO (mínimo 10 caracteres)");

        // DEC-R005/R006: snapshot IA del último análisis completado (o NULL si no existe).
        var (goNoGo, score) = await handler.RecomendacionAnalisisAsync(licitacionId, ct);

        await handler.RegistrarAsync(licitacionId, decision, motivo, goNoGo, score, decididoPor, ct);

        // DEC-R007: decidido_por viene del JWT; decidido_at lo pone la BD (CURRENT_TIMESTAMP).
        return new DecisionDto
        {
            CodigoExterno = codigoExterno,
            Decision = decision,
            Motivo = motivo,
            RecomendacionIa = goNoGo,
            ScoreConfianza = score,
            DecididoPor = decididoPor,
            DecididoAt = DateTime.UtcNow,
            Notificados = null, // DEC-R010: Fase 3.
        };
    }

    public virtual async Task<DecisionEstadoDto> ObtenerAsync(long licitacionId, CancellationToken ct = default)
    {
        var row = await handler.ObtenerAsync(licitacionId, ct);
        if (row == null)
        {
            return new DecisionEstadoDto
            {
                Decidida = false,
                Decision = null,
                Motivo = null,
                RecomendacionIa = null,
                ScoreConfianza = null,
                DecididoPor = null,
                DecididoAt = null,
                Notificados = null,
            };
        }

        return new DecisionEstadoDto
        {
            Decidida = !string.IsNullOrWhiteSpace(row.Decision),
            Decision = row.Decision,
            Motivo = row.Motivo,
            RecomendacionIa = row.RecomendacionIa,
            ScoreConfianza = row.ScoreConfianza,
            DecididoPor = row.DecididoPor,
            DecididoAt = row.DecididoAt,
            Notificados = ParseNotificados(row.Notificados),
        };
    }

    /// <summary>notificados es JSONB (lista de strings); queda null hasta Fase 3 (DEC-R010).</summary>
    private static List<string>? ParseNotificados(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
