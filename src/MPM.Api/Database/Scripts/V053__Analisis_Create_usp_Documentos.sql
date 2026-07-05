CREATE OR REPLACE FUNCTION usp_AnalisisDocumentos_Crear(
    p_workspace_id BIGINT,
    p_nombre_archivo VARCHAR(500),
    p_mime_type VARCHAR(100),
    p_tamanio_bytes BIGINT,
    p_ruta_storage TEXT,
    OUT p_id BIGINT,
    OUT p_error_msg TEXT
) RETURNS RECORD AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM analisis_workspaces WHERE id = p_workspace_id AND record_status = 1) THEN
        p_error_msg := 'ANA_001:Workspace no encontrado';
        RETURN;
    END IF;

    IF p_mime_type != 'application/pdf' THEN
        p_error_msg := 'VAL_004:Solo se permiten archivos PDF';
        RETURN;
    END IF;

    INSERT INTO analisis_documentos (workspace_id, nombre_archivo, mime_type, tamanio_bytes, ruta_storage)
    VALUES (p_workspace_id, p_nombre_archivo, p_mime_type, p_tamanio_bytes, p_ruta_storage)
    RETURNING id INTO p_id;

    UPDATE analisis_workspaces
    SET estado = CASE WHEN estado = 'pendiente' THEN 'listo' ELSE estado END,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = p_workspace_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_AnalisisDocumentos_Listar(
    p_workspace_id BIGINT
) RETURNS TABLE(
    id BIGINT,
    nombre_archivo VARCHAR,
    mime_type VARCHAR,
    tamanio_bytes BIGINT,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT ad.id, ad.nombre_archivo, ad.mime_type, ad.tamanio_bytes, ad.created_at
    FROM analisis_documentos ad
    WHERE ad.workspace_id = p_workspace_id AND ad.record_status = 1
    ORDER BY ad.created_at DESC;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_AnalisisDocumentos_Obtener(
    p_id BIGINT
) RETURNS TABLE(
    id BIGINT,
    workspace_id BIGINT,
    nombre_archivo VARCHAR,
    mime_type VARCHAR,
    tamanio_bytes BIGINT,
    ruta_storage TEXT,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT ad.id, ad.workspace_id, ad.nombre_archivo, ad.mime_type,
           ad.tamanio_bytes, ad.ruta_storage, ad.created_at
    FROM analisis_documentos ad
    WHERE ad.id = p_id AND ad.record_status = 1;
END;
$$ LANGUAGE plpgsql;
