namespace MPM.Modules.Colaboracion.Data;

/// <summary>
/// SPs de la decisión GO/NO GO (V144 — 036-flujo-comercial-ofertas, Fase 2).
/// La decisión vive sobre licitaciones_interes (evolución V122), 1 fila por licitación.
/// </summary>
public static class DecisionStoredProcedures
{
    public const string Registrar =
        "CALL usp_LicitacionesDecision_Registrar(@p_licitacion_id, @p_decision, @p_motivo, @p_recomendacion_ia, @p_score_confianza, @p_decidido_por, @p_id, @p_error_msg)";

    public const string Obtener =
        "SELECT * FROM usp_LicitacionesDecision_Obtener(@p_licitacion_id)";
}
