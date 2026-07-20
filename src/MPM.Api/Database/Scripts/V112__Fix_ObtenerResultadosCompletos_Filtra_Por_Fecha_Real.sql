-- V112: 029-fix-hallazgos-code-review-competidores-alertas (FR-018/US14, QA BUG-011)
-- usp_Analisis_ObtenerResultadosCompletos filtraba p_anio contra ar.created_at (fecha de
-- creación del registro de análisis), no contra la fecha real de la licitación -- una
-- licitación de 2025 analizada recién en 2026 nunca aparecía al filtrar por "2025" en el
-- Dashboard Ejecutivo. Se filtra ahora por el año de licitacion.fechas.adjudicacion
-- (preferida) o licitacion.fechas.publicacion dentro de contenido_json, con created_at
-- como último fallback cuando el JSON no trae ninguna fecha real (o trae un formato no
-- parseable, p.ej si Gemini alguna vez no respeta el "YYYY-MM-DD" del prompt) -- mismo
-- orden de precedencia que AnalisisService.ExtraerAnioRealLicitacion en el backend. El
-- guard con "~ '^\d{4}-\d{2}-\d{2}'" evita que un CAST a DATE reviente toda la consulta
-- por una sola fila con formato inesperado.

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
      AND (
        p_anio IS NULL
        OR EXTRACT(YEAR FROM COALESCE(
             CASE WHEN ar.contenido_json #>> '{licitacion,fechas,adjudicacion}' ~ '^\d{4}-\d{2}-\d{2}'
                  THEN (ar.contenido_json #>> '{licitacion,fechas,adjudicacion}')::DATE END,
             CASE WHEN ar.contenido_json #>> '{licitacion,fechas,publicacion}' ~ '^\d{4}-\d{2}-\d{2}'
                  THEN (ar.contenido_json #>> '{licitacion,fechas,publicacion}')::DATE END,
             ar.created_at::DATE
           ))::INT = p_anio
      )
    ORDER BY ar.created_at DESC;
END;
$$ LANGUAGE plpgsql;
