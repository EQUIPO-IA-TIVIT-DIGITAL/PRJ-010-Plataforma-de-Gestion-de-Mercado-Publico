-- Fix V052: column reference "id" is ambiguous in usp_AnalisisWorkspaces_Listar

CREATE OR REPLACE FUNCTION usp_AnalisisWorkspaces_Listar(
    p_page INTEGER DEFAULT 1,
    p_page_size INTEGER DEFAULT 20,
    p_search VARCHAR DEFAULT NULL,
    p_estado VARCHAR DEFAULT NULL
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
        JOIN licitaciones l ON l.id = aw.licitacion_id
        WHERE aw.record_status = 1
        AND (p_search IS NULL OR aw.nombre ILIKE '%' || p_search || '%')
        AND (p_estado IS NULL OR aw.estado = p_estado)
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
