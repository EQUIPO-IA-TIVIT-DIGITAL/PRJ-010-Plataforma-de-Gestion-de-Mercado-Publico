namespace MPM.Modules.Licitaciones.Data;

/// <summary>SPs/funciones del análisis comercial de licitación (V142 — 036-flujo-comercial-ofertas).</summary>
public static class AnalisisComercialStoredProcedures
{
    public const string ObtenerUltimo =
        "SELECT * FROM usp_AnalisisComercial_ObtenerUltimo(@p_licitacion_id)";

    public const string Iniciar =
        "CALL usp_AnalisisComercial_Iniciar(@p_licitacion_id, @p_conjunto_hash, @p_creado_por, @p_id, @p_ya_existia, @p_error_msg)";

    public const string Completar =
        "CALL usp_AnalisisComercial_Completar(@p_id, @p_estado, @p_resultado_json, @p_resumen_ejecutivo, @p_go_no_go, @p_score_confianza, @p_modelo_usado, @p_tokens_entrada, @p_tokens_salida, @p_error, @p_error_msg)";
}
