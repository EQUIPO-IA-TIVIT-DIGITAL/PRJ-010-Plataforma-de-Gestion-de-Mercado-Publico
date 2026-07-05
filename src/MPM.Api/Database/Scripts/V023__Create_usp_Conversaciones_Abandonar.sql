CREATE OR REPLACE PROCEDURE usp_Conversaciones_Abandonar(
    IN p_id BIGINT,
    IN p_user_id VARCHAR(100),
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_remaining INT;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM conversaciones WHERE id = p_id AND deleted_at IS NULL) THEN
        p_error_msg := 'MSG_001: Conversacion no encontrada';
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = p_id AND user_id = p_user_id AND left_at IS NULL
    ) THEN
        p_error_msg := 'AUTH_001: No es participante de la conversacion';
        RETURN;
    END IF;

    UPDATE conversacion_participantes
    SET left_at = CURRENT_TIMESTAMP
    WHERE conversacion_id = p_id AND user_id = p_user_id AND left_at IS NULL;

    INSERT INTO mensajes (conversacion_id, user_id, tipo, contenido)
    VALUES (p_id, 'system', 'sistema', p_user_id || ' abandono la conversacion');

    SELECT COUNT(*) INTO v_remaining
    FROM conversacion_participantes
    WHERE conversacion_id = p_id AND left_at IS NULL;

    IF v_remaining = 0 THEN
        UPDATE conversaciones SET deleted_at = CURRENT_TIMESTAMP WHERE id = p_id;
    END IF;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
