CREATE OR REPLACE FUNCTION usp_AnalisisChat_CrearConversacion(
    p_workspace_id BIGINT,
    OUT p_id BIGINT,
    OUT p_error_msg TEXT
) RETURNS RECORD AS $$
BEGIN
    INSERT INTO analisis_chat_conversaciones (workspace_id)
    VALUES (p_workspace_id)
    RETURNING id INTO p_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_AnalisisChat_ObtenerOCrearConversacion(
    p_workspace_id BIGINT,
    OUT p_conversacion_id BIGINT,
    OUT p_error_msg TEXT
) RETURNS RECORD AS $$
BEGIN
    SELECT id INTO p_conversacion_id
    FROM analisis_chat_conversaciones
    WHERE workspace_id = p_workspace_id AND record_status = 1
    ORDER BY created_at ASC
    LIMIT 1;

    IF p_conversacion_id IS NULL THEN
        INSERT INTO analisis_chat_conversaciones (workspace_id)
        VALUES (p_workspace_id)
        RETURNING id INTO p_conversacion_id;
    END IF;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_AnalisisChat_EnviarMensaje(
    p_conversacion_id BIGINT,
    p_rol VARCHAR(10),
    p_contenido TEXT,
    OUT p_id BIGINT,
    OUT p_error_msg TEXT
) RETURNS RECORD AS $$
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
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_AnalisisChat_ObtenerHistorial(
    p_conversacion_id BIGINT,
    p_limit INTEGER DEFAULT 50
) RETURNS TABLE(
    id BIGINT,
    rol VARCHAR,
    contenido TEXT,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT acm.id, acm.rol, acm.contenido, acm.created_at
    FROM analisis_chat_mensajes acm
    WHERE acm.conversacion_id = p_conversacion_id
    ORDER BY acm.created_at ASC
    LIMIT p_limit;
END;
$$ LANGUAGE plpgsql;
