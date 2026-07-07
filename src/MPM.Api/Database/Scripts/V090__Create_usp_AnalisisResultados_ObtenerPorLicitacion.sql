-- T029 parcial (003-fase6-alertas-keywords): permite que Alertas reutilice un analisis de
-- Gemini ya existente para el resumen enriquecido, buscando por licitacion_id en vez de
-- workspace_id (que es lo que ya tenia usp_AnalisisResultados_ObtenerPorWorkspace, V054).
-- Devuelve el resultado mas reciente si existen varios (mismo patron que V054), o ninguna
-- fila si la licitacion nunca paso por un workspace de Analisis.
CREATE OR REPLACE FUNCTION usp_AnalisisResultados_ObtenerPorLicitacion(
    p_licitacion_id BIGINT
) RETURNS TABLE(
    id BIGINT,
    workspace_id BIGINT,
    documento_id BIGINT,
    documento_nombre VARCHAR,
    contenido_json JSONB,
    modelo_usado VARCHAR,
    tokens_entrada INTEGER,
    tokens_salida INTEGER,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT ar.id, ar.workspace_id, ar.documento_id, ad.nombre_archivo,
           ar.contenido_json, ar.modelo_usado, ar.tokens_entrada, ar.tokens_salida, ar.created_at
    FROM analisis_resultados ar
    JOIN analisis_documentos ad ON ad.id = ar.documento_id
    JOIN analisis_workspaces aw ON aw.id = ar.workspace_id
    WHERE aw.licitacion_id = p_licitacion_id
    ORDER BY ar.created_at DESC
    LIMIT 1;
END;
$$ LANGUAGE plpgsql;
