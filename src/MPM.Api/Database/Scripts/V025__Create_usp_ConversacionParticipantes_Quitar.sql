CREATE OR REPLACE PROCEDURE usp_ConversacionParticipantes_Quitar(
    IN p_conversacion_id BIGINT,
    IN p_user_id VARCHAR(100),
    IN p_solicitante_id VARCHAR(100),
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_remaining INT;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM conversaciones WHERE id = p_conversacion_id AND deleted_at IS NULL) THEN
        p_error_msg := 'MSG_001: Conversacion no encontrada';
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = p_conversacion_id AND user_id = p_solicitante_id AND rol = 'admin' AND left_at IS NULL
    ) THEN
        p_error_msg := 'AUTH_001: No tiene permisos para realizar esta accion';
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = p_conversacion_id AND user_id = p_user_id AND left_at IS NULL
    ) THEN
        p_error_msg := 'MSG_005: El usuario no es participante de la conversacion';
        RETURN;
    END IF;

    SELECT COUNT(*) INTO v_remaining
    FROM conversacion_participantes
    WHERE conversacion_id = p_conversacion_id AND left_at IS NULL;

    IF v_remaining <= 1 THEN
        p_error_msg := 'MSG_003: No se puede quitar al ultimo participante, use abandonar';
        RETURN;
    END IF;

    UPDATE conversacion_participantes
    SET left_at = CURRENT_TIMESTAMP
    WHERE conversacion_id = p_conversacion_id AND user_id = p_user_id AND left_at IS NULL;

    UPDATE conversaciones SET updated_at = CURRENT_TIMESTAMP WHERE id = p_conversacion_id;

    INSERT INTO mensajes (conversacion_id, user_id, tipo, contenido)
    VALUES (p_conversacion_id, 'system', 'sistema', p_user_id || ' fue removido de la conversacion');

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
