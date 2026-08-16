namespace MPM.Modules.Licitaciones.Data;

/// <summary>
/// SPs/funciones de documentos de licitación (V141 — 036-flujo-comercial-ofertas).
/// Escrituras vía PROCEDURE (CALL), lecturas vía función (SELECT * FROM ...).
/// </summary>
public static class AdjuntoDocumentosStoredProcedures
{
    public const string ListarPorLicitacion =
        "SELECT * FROM usp_Adjuntos_ListarPorLicitacion(@p_licitacion_id)";

    public const string MarcarDescargaIniciada =
        "CALL usp_Adjuntos_MarcarDescargaIniciada(@p_licitacion_id, @p_iniciada_por, @p_error_msg)";

    public const string MarcarDescargaFinalizada =
        "CALL usp_Adjuntos_MarcarDescargaFinalizada(@p_licitacion_id, @p_estado, @p_error, @p_error_msg)";

    /// <summary>Descargas en curso "vivas" (menos de 10 min sin actualizar) para idempotencia.</summary>
    public const string ExistenDescargasVivas =
        """
        SELECT COUNT(*)::INT
        FROM licitaciones_adjuntos
        WHERE licitacion_id = @p_licitacion_id
          AND record_status = 1
          AND descarga_estado = 'descargando'
          AND updated_at > CURRENT_TIMESTAMP - INTERVAL '10 minutes'
        """;
}
