CREATE OR REPLACE PROCEDURE usp_Mensajes_Enviar(
    IN p_conversacion_id BIGINT,
    IN p_user_id VARCHAR(100),
    IN p_tipo VARCHAR(10),
    IN p_contenido TEXT,
    IN p_reply_to_id BIGINT,
    INOUT p_id BIGINT DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM conversaciones WHERE id = p_conversacion_id AND deleted_at IS NULL) THEN
        p_error_msg := 'MSG_001: Conversacion no encontrada';
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = p_conversacion_id AND user_id = p_user_id AND left_at IS NULL
    ) THEN
        p_error_msg := 'AUTH_001: No es participante activo de la conversacion';
        RETURN;
    END IF;

    IF p_tipo = 'texto' AND (p_contenido IS NULL OR TRIM(p_contenido) = '') THEN
        p_error_msg := 'VAL_001: contenido es requerido para mensajes de texto';
        RETURN;
    END IF;

    IF p_contenido IS NOT NULL AND LENGTH(p_contenido) > 5000 THEN
        p_error_msg := 'VAL_008: contenido excede el largo maximo de 5000 caracteres';
        RETURN;
    END IF;

    INSERT INTO mensajes (conversacion_id, user_id, tipo, contenido, reply_to_id)
    VALUES (p_conversacion_id, p_user_id, p_tipo, p_contenido, p_reply_to_id)
    RETURNING id INTO p_id;

    UPDATE conversaciones SET updated_at = CURRENT_TIMESTAMP WHERE id = p_conversacion_id;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
