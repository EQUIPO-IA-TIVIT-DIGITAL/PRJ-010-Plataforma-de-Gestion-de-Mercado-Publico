-- V071: SP para obtener todos los resultados de análisis completados
-- Usado por el Dashboard Ejecutivo (Fase 3 / US2)

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
    SELECT
        aw.id             AS workspace_id,
        aw.nombre         AS workspace_nombre,
        aw.licitacion_id  AS licitacion_id,
        ar.modelo_usado   AS modelo_usado,
        ar.tokens_entrada AS tokens_entrada,
        ar.tokens_salida  AS tokens_salida,
        ar.created_at     AS creado_en,
        ar.contenido_json AS contenido_json
    FROM analisis_resultados ar
    JOIN analisis_workspaces aw ON aw.id = ar.workspace_id
    WHERE aw.estado = 'completado'
      AND aw.record_status = 1
      AND (p_anio IS NULL OR EXTRACT(YEAR FROM ar.created_at)::INT = p_anio)
    ORDER BY ar.created_at DESC;
END;
$$ LANGUAGE plpgsql;
