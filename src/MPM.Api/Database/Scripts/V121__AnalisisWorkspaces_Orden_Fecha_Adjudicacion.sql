-- V121: US3 (spec 031) — el historial de análisis debe ordenarse por la fecha de
-- adjudicación de la licitación asociada, no por cuándo se generó el análisis.
-- Base: V113. Agrega fecha_adjudicacion a la proyección y cambia el ORDER BY a
-- COALESCE(fecha_adjudicacion, fecha_estimada_adjudicacion) DESC NULLS LAST,
-- created_at DESC (análisis sin fecha de adjudicación registrada quedan al final,
-- ver contracts/analisis-orden.md).

-- CREATE OR REPLACE no permite agregar una columna a RETURNS TABLE (error 42P13:
-- "cannot change return type of existing function", confirmado en vivo contra Postgres
-- real de docker-compose) -- a diferencia de agregar parámetros de ENTRADA (como hizo
-- V119 sobre usp_Licitaciones_Listar), un cambio en las columnas de SALIDA exige DROP.
DROP FUNCTION IF EXISTS usp_AnalisisWorkspaces_Listar(INTEGER, INTEGER, VARCHAR, VARCHAR, DATE, DATE);

CREATE OR REPLACE FUNCTION usp_AnalisisWorkspaces_Listar(
    p_page INTEGER DEFAULT 1,
    p_page_size INTEGER DEFAULT 20,
    p_search VARCHAR DEFAULT NULL,
    p_estado VARCHAR DEFAULT NULL,
    p_fecha_desde DATE DEFAULT NULL,
    p_fecha_hasta DATE DEFAULT NULL
) RETURNS TABLE(
    id BIGINT,
    licitacion_id BIGINT,
    licitacion_nombre VARCHAR,
    nombre VARCHAR,
    estado VARCHAR,
    documentos_count BIGINT,
    ultimo_analisis_id BIGINT,
    ultimo_analisis_fecha TIMESTAMP,
    created_at TIMESTAMP,
    fecha_adjudicacion TIMESTAMP,
    totalcount BIGINT
) AS $$
BEGIN
    RETURN QUERY
    WITH filtered AS (
        SELECT aw.*, l.nombre AS lic_nombre,
               COALESCE(l.fecha_adjudicacion, l.fecha_estimada_adjudicacion) AS lic_fecha_adjudicacion
        FROM analisis_workspaces aw
        LEFT JOIN licitaciones l ON l.id = aw.licitacion_id
        WHERE aw.record_status = 1
        AND (p_search IS NULL OR aw.nombre ILIKE '%' || p_search || '%')
        AND (p_estado IS NULL OR aw.estado = p_estado)
        AND (p_fecha_desde IS NULL OR aw.created_at >= p_fecha_desde)
        AND (p_fecha_hasta IS NULL OR aw.created_at < (p_fecha_hasta + INTERVAL '1 day'))
    ),
    doc_counts AS (
        SELECT ad.workspace_id, COUNT(*) AS cnt FROM analisis_documentos ad
        WHERE ad.record_status = 1 GROUP BY ad.workspace_id
    ),
    last_results AS (
        SELECT DISTINCT ON (ar.workspace_id) ar.workspace_id, ar.id, ar.created_at
        FROM analisis_resultados ar
        ORDER BY ar.workspace_id, ar.created_at DESC
    )
    SELECT
        f.id,
        f.licitacion_id,
        f.lic_nombre,
        f.nombre,
        f.estado,
        COALESCE(dc.cnt, 0),
        lr.id,
        lr.created_at,
        f.created_at,
        f.lic_fecha_adjudicacion,
        COUNT(*) OVER() AS totalcount
    FROM filtered f
    LEFT JOIN doc_counts dc ON dc.workspace_id = f.id
    LEFT JOIN last_results lr ON lr.workspace_id = f.id
    ORDER BY f.lic_fecha_adjudicacion DESC NULLS LAST, f.created_at DESC
    LIMIT p_page_size OFFSET (p_page - 1) * p_page_size;
END;
$$ LANGUAGE plpgsql;
