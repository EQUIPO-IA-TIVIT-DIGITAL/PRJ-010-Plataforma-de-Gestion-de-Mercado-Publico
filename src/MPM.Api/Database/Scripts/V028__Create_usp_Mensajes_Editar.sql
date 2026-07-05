CREATE OR REPLACE PROCEDURE usp_Mensajes_Editar(
    IN p_id BIGINT,
    IN p_user_id VARCHAR(100),
    IN p_contenido TEXT,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_msg RECORD;
BEGIN
    SELECT m.id, m.user_id, m.created_at, m.deleted_at
    INTO v_msg
    FROM mensajes m
    WHERE m.id = p_id;

    IF v_msg IS NULL OR v_msg.deleted_at IS NOT NULL THEN
        p_error_msg := 'MSG_006: Mensaje no encontrado';
        RETURN;
    END IF;

    IF v_msg.user_id != p_user_id THEN
        p_error_msg := 'AUTH_001: Solo el autor puede editar el mensaje';
        RETURN;
    END IF;

    IF v_msg.created_at < (CURRENT_TIMESTAMP - INTERVAL '15 minutes') THEN
        p_error_msg := 'MSG_003: La ventana de edicion de 15 minutos ha expirado';
        RETURN;
    END IF;

    IF p_contenido IS NULL OR TRIM(p_contenido) = '' THEN
        p_error_msg := 'VAL_001: contenido es requerido';
        RETURN;
    END IF;

    IF LENGTH(p_contenido) > 5000 THEN
        p_error_msg := 'VAL_008: contenido excede el largo maximo de 5000 caracteres';
        RETURN;
    END IF;

    UPDATE mensajes
    SET contenido = p_contenido, edited_at = CURRENT_TIMESTAMP
    WHERE id = p_id;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
