-- Make licitacion_id nullable in analisis_workspaces
ALTER TABLE analisis_workspaces DROP CONSTRAINT IF EXISTS analisis_workspaces_licitacion_id_fkey;
ALTER TABLE analisis_workspaces ALTER COLUMN licitacion_id DROP NOT NULL;
ALTER TABLE analisis_workspaces ADD CONSTRAINT analisis_workspaces_licitacion_id_fkey
    FOREIGN KEY (licitacion_id) REFERENCES licitaciones(id);

DROP FUNCTION IF EXISTS usp_AnalisisWorkspaces_Crear(p_licitacion_id bigint, p_nombre character varying, p_user_id character varying, OUT p_id bigint, OUT p_error_msg text);

CREATE OR REPLACE PROCEDURE usp_AnalisisWorkspaces_Crear(
    p_licitacion_id BIGINT DEFAULT NULL,
    p_nombre VARCHAR(200) DEFAULT '',
    p_user_id VARCHAR(50) DEFAULT '',
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_nombre IS NULL OR trim(p_nombre) = '' THEN
        p_error_msg := 'VAL_001:nombre es requerido';
        RETURN;
    END IF;

    IF p_licitacion_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM licitaciones WHERE id = p_licitacion_id AND deleted_at IS NULL) THEN
        p_error_msg := 'VAL_006:licitacionId no encontrado';
        RETURN;
    END IF;

    INSERT INTO analisis_workspaces (licitacion_id, nombre, user_id)
    VALUES (p_licitacion_id, trim(p_nombre), p_user_id)
    RETURNING id INTO p_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- Fix usp_AnalisisWorkspaces_Listar and Obtener for nullable licitacion_id
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
        LEFT JOIN licitaciones l ON l.id = aw.licitacion_id
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

CREATE OR REPLACE FUNCTION usp_AnalisisWorkspaces_Obtener(
    p_id BIGINT
) RETURNS TABLE(
    id BIGINT,
    licitacion_id BIGINT,
    licitacion_nombre VARCHAR,
    nombre VARCHAR,
    estado VARCHAR,
    documentos_count BIGINT,
    ultimo_analisis_id BIGINT,
    ultimo_analisis_documento_id BIGINT,
    ultimo_analisis_documento_nombre VARCHAR,
    ultimo_analisis_fecha TIMESTAMP,
    created_at TIMESTAMP,
    updated_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        aw.id,
        aw.licitacion_id,
        l.nombre,
        aw.nombre,
        aw.estado,
        (SELECT COUNT(*) FROM analisis_documentos ad WHERE ad.workspace_id = aw.id AND ad.record_status = 1),
        ar.id,
        ar.documento_id,
        ad.nombre_archivo,
        ar.created_at,
        aw.created_at,
        aw.updated_at
    FROM analisis_workspaces aw
    LEFT JOIN licitaciones l ON l.id = aw.licitacion_id
    LEFT JOIN LATERAL (
        SELECT * FROM analisis_resultados
        WHERE workspace_id = aw.id
        ORDER BY created_at DESC LIMIT 1
    ) ar ON true
    LEFT JOIN analisis_documentos ad ON ad.id = ar.documento_id
    WHERE aw.id = p_id AND aw.record_status = 1;
END;
$$ LANGUAGE plpgsql;
