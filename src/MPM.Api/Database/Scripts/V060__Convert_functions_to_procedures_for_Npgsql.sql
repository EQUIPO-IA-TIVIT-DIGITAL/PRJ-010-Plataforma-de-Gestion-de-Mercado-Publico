-- V060: Convert functions with OUT params to procedures for Npgsql 8.x compatibility.
-- Npgsql 8.x CommandType.StoredProcedure cannot resolve PostgreSQL functions that
-- have OUT parameters (RETURNS RECORD). Converting them to PROCEDUREs with INOUT
-- params resolves the issue when called via CALL with DynamicParameters.
-- First drop the old FUNCTION overloads that conflict with the new PROCEDUREs.

DROP FUNCTION IF EXISTS usp_AnalisisDocumentos_Crear(bigint, character varying, character varying, bigint, text);
DROP FUNCTION IF EXISTS usp_AnalisisResultados_Crear(bigint, bigint, jsonb, character varying, integer, integer);
DROP FUNCTION IF EXISTS usp_AnalisisChat_CrearConversacion(bigint);
DROP FUNCTION IF EXISTS usp_AnalisisChat_ObtenerOCrearConversacion(bigint);
DROP FUNCTION IF EXISTS usp_AnalisisChat_EnviarMensaje(bigint, character varying, text);
DROP FUNCTION IF EXISTS usp_AnalisisWorkspaces_ActualizarEstado(bigint, character varying);
DROP FUNCTION IF EXISTS usp_AnalisisWorkspaces_Eliminar(bigint);

CREATE OR REPLACE PROCEDURE usp_AnalisisDocumentos_Crear(
    IN p_workspace_id BIGINT,
    IN p_nombre_archivo VARCHAR(500),
    IN p_mime_type VARCHAR(100),
    IN p_tamanio_bytes BIGINT,
    IN p_ruta_storage TEXT,
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
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
$$;

CREATE OR REPLACE PROCEDURE usp_AnalisisResultados_Crear(
    IN p_workspace_id BIGINT,
    IN p_documento_id BIGINT,
    IN p_contenido_json JSONB,
    IN p_modelo_usado VARCHAR(100),
    IN p_tokens_entrada INTEGER,
    IN p_tokens_salida INTEGER,
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO analisis_resultados (workspace_id, documento_id, contenido_json, modelo_usado, tokens_entrada, tokens_salida)
    VALUES (p_workspace_id, p_documento_id, p_contenido_json, p_modelo_usado, p_tokens_entrada, p_tokens_salida)
    RETURNING id INTO p_id;

    UPDATE analisis_workspaces
    SET estado = 'completado', updated_at = CURRENT_TIMESTAMP, last_analyzed_at = CURRENT_TIMESTAMP
    WHERE id = p_workspace_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_AnalisisChat_CrearConversacion(
    IN p_workspace_id BIGINT,
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO analisis_chat_conversaciones (workspace_id)
    VALUES (p_workspace_id)
    RETURNING id INTO p_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_AnalisisChat_ObtenerOCrearConversacion(
    IN p_workspace_id BIGINT,
    INOUT p_conversacion_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT id INTO p_conversacion_id
    FROM analisis_chat_conversaciones
    WHERE workspace_id = p_workspace_id AND record_status = 1
    ORDER BY created_at ASC
    LIMIT 1;

    IF p_conversacion_id IS NULL OR p_conversacion_id = 0 THEN
        INSERT INTO analisis_chat_conversaciones (workspace_id)
        VALUES (p_workspace_id)
        RETURNING id INTO p_conversacion_id;
    END IF;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_AnalisisChat_EnviarMensaje(
    IN p_conversacion_id BIGINT,
    IN p_rol VARCHAR(10),
    IN p_contenido TEXT,
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_rol NOT IN ('user', 'assistant') THEN
        p_error_msg := 'VAL_001:rol debe ser user o assistant';
        RETURN;
    END IF;

    IF p_contenido IS NULL OR trim(p_contenido) = '' THEN
        p_error_msg := 'VAL_001:contenido es requerido';
        RETURN;
    END IF;

    INSERT INTO analisis_chat_mensajes (conversacion_id, rol, contenido)
    VALUES (p_conversacion_id, p_rol, p_contenido)
    RETURNING id INTO p_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_AnalisisWorkspaces_ActualizarEstado(
    IN p_id BIGINT,
    IN p_estado VARCHAR,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
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
$$;

CREATE OR REPLACE PROCEDURE usp_AnalisisWorkspaces_Eliminar(
    IN p_id BIGINT,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
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
$$;
