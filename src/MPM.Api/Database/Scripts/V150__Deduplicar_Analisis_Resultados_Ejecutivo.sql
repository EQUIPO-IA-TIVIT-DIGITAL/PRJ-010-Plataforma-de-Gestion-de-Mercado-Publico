-- V150: Deduplicación de análisis de licitaciones en Dashboard Ejecutivo.
-- Un workspace puede tener múltiples análisis_resultados (por múltiples ejecuciones o documentos),
-- y una misma licitación puede tener más de un workspace. Esta función asegura que cada
-- licitación (o workspace independiente) se procese exactamente UNA vez, tomando su análisis
-- completado más reciente.

CREATE OR REPLACE FUNCTION usp_Analisis_ObtenerResultadosCompletos(
    p_anio INT DEFAULT NULL
)
RETURNS TABLE (
    workspace_id       BIGINT,
    workspace_nombre   VARCHAR,
    licitacion_id      BIGINT,
    modelo_usado       VARCHAR,
    tokens_entrada     INT,
    tokens_salida      INT,
    creado_en          TIMESTAMP,
    contenido_json     JSONB
) AS $$
BEGIN
    RETURN QUERY
    WITH ultimos_resultados_por_workspace AS (
        -- 1. Obtiene solo el análisis más reciente por cada workspace
        SELECT DISTINCT ON (ar.workspace_id)
            ar.id,
            ar.workspace_id,
            ar.modelo_usado,
            ar.tokens_entrada,
            ar.tokens_salida,
            ar.created_at,
            ar.contenido_json
        FROM analisis_resultados ar
        ORDER BY ar.workspace_id, ar.created_at DESC
    ),
    ultimos_por_licitacion_o_workspace AS (
        -- 2. Si varios workspaces corresponden a la misma licitación, toma el workspace más reciente
        SELECT DISTINCT ON (COALESCE(aw.licitacion_id::TEXT, 'ws_' || aw.id::TEXT))
            aw.id             AS workspace_id,
            aw.nombre         AS workspace_nombre,
            aw.licitacion_id  AS licitacion_id,
            ur.modelo_usado   AS modelo_usado,
            ur.tokens_entrada AS tokens_entrada,
            ur.tokens_salida  AS tokens_salida,
            ur.created_at     AS creado_en,
            ur.contenido_json AS contenido_json
        FROM ultimos_resultados_por_workspace ur
        JOIN analisis_workspaces aw ON aw.id = ur.workspace_id
        WHERE aw.estado = 'completado'
          AND aw.record_status = 1
        ORDER BY COALESCE(aw.licitacion_id::TEXT, 'ws_' || aw.id::TEXT), ur.created_at DESC
    )
    SELECT
        u.workspace_id,
        u.workspace_nombre,
        u.licitacion_id,
        u.modelo_usado,
        u.tokens_entrada,
        u.tokens_salida,
        u.creado_en,
        u.contenido_json
    FROM ultimos_por_licitacion_o_workspace u
    WHERE (
        p_anio IS NULL
        OR EXTRACT(YEAR FROM COALESCE(
             CASE WHEN u.contenido_json #>> '{licitacion,fechas,adjudicacion}' ~ '^\d{4}-\d{2}-\d{2}'
                  THEN (u.contenido_json #>> '{licitacion,fechas,adjudicacion}')::DATE END,
             CASE WHEN u.contenido_json #>> '{licitacion,fechas,publicacion}' ~ '^\d{4}-\d{2}-\d{2}'
                  THEN (u.contenido_json #>> '{licitacion,fechas,publicacion}')::DATE END,
             u.creado_en::DATE
           ))::INT = p_anio
    )
    ORDER BY u.creado_en DESC;
END;
$$ LANGUAGE plpgsql;
