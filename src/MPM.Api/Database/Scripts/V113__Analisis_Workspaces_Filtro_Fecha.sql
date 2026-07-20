-- 030-qol-frontend-y-fix-scraper US4: filtrar /analisis por rango de fechas.
-- Extiende usp_AnalisisWorkspaces_Listar con p_fecha_desde/p_fecha_hasta opcionales, filtrando
-- sobre aw.created_at. El orden (created_at DESC) no cambia.
--
-- Base tomada de V059 (no de V052): V052 tenia el bug "column reference id is ambiguous"
-- (RETURNS TABLE(id BIGINT, ...) declara "id" como variable PL/pgSQL, que colisiona con la
-- columna analisis_resultados.id referenciada sin calificar dentro de la CTE last_results).
-- V058 lo corrigio calificando ar.id/ar.workspace_id, y V059 ademas cambio el JOIN a
-- licitaciones por LEFT JOIN (licitacion_id es nullable). Copiar V052 directo reintroducia
-- ambos bugs -- se detecto en vivo contra Postgres real de docker-compose antes de mergear.

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
    totalcount BIGINT
) AS $$
BEGIN
    RETURN QUERY
    WITH filtered AS (
        SELECT aw.*, l.nombre AS lic_nombre
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
        COUNT(*) OVER() AS totalcount
    FROM filtered f
    LEFT JOIN doc_counts dc ON dc.workspace_id = f.id
    LEFT JOIN last_results lr ON lr.workspace_id = f.id
    ORDER BY f.created_at DESC
    LIMIT p_page_size OFFSET (p_page - 1) * p_page_size;
END;
$$ LANGUAGE plpgsql;
