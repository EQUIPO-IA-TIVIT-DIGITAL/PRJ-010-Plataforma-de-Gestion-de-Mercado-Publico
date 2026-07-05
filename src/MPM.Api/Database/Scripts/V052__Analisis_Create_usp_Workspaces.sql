CREATE OR REPLACE FUNCTION usp_AnalisisWorkspaces_Crear(
    p_licitacion_id BIGINT,
    p_nombre VARCHAR(200),
    p_user_id VARCHAR(50),
    OUT p_id BIGINT,
    OUT p_error_msg TEXT
) RETURNS RECORD AS $$
BEGIN
    IF p_nombre IS NULL OR trim(p_nombre) = '' THEN
        p_error_msg := 'VAL_001:nombre es requerido';
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM licitaciones WHERE id = p_licitacion_id AND record_status = 1) THEN
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
$$ LANGUAGE plpgsql;


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
        SELECT workspace_id, COUNT(*) AS cnt FROM analisis_documentos
        WHERE record_status = 1 GROUP BY workspace_id
    ),
    last_results AS (
        SELECT DISTINCT ON (workspace_id) workspace_id, id, created_at
        FROM analisis_resultados
        ORDER BY workspace_id, created_at DESC
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
    JOIN licitaciones l ON l.id = aw.licitacion_id
    LEFT JOIN LATERAL (
        SELECT * FROM analisis_resultados
        WHERE workspace_id = aw.id
        ORDER BY created_at DESC LIMIT 1
    ) ar ON true
    LEFT JOIN analisis_documentos ad ON ad.id = ar.documento_id
    WHERE aw.id = p_id AND aw.record_status = 1;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_AnalisisWorkspaces_ActualizarEstado(
    p_id BIGINT,
    p_estado VARCHAR,
    OUT p_error_msg TEXT
) RETURNS TEXT AS $$
BEGIN
    IF p_estado NOT IN ('pendiente', 'listo', 'analizando', 'completado', 'error') THEN
        p_error_msg := 'VAL_001:estado inválido';
        RETURN;
    END IF;

    UPDATE analisis_workspaces
    SET estado = p_estado,
        updated_at = CURRENT_TIMESTAMP,
        last_analyzed_at = CASE WHEN p_estado = 'completado' THEN CURRENT_TIMESTAMP ELSE last_analyzed_at END
    WHERE id = p_id AND record_status = 1;

    IF NOT FOUND THEN
        p_error_msg := 'ANA_001:Workspace no encontrado';
        RETURN;
    END IF;

    p_error_msg := NULL;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_AnalisisWorkspaces_Eliminar(
    p_id BIGINT,
    OUT p_error_msg TEXT
) RETURNS TEXT AS $$
DECLARE
    v_estado VARCHAR;
BEGIN
    SELECT estado INTO v_estado FROM analisis_workspaces WHERE id = p_id AND record_status = 1;

    IF v_estado IS NULL THEN
        p_error_msg := 'ANA_001:Workspace no encontrado';
        RETURN;
    END IF;

    IF v_estado = 'analizando' THEN
        p_error_msg := 'ANA_005:No se puede eliminar un workspace con análisis en progreso';
        RETURN;
    END IF;

    UPDATE analisis_workspaces SET record_status = 0, updated_at = CURRENT_TIMESTAMP WHERE id = p_id;
    p_error_msg := NULL;
END;
$$ LANGUAGE plpgsql;
