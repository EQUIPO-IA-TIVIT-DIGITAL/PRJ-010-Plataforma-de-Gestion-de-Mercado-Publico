CREATE OR REPLACE PROCEDURE usp_Mensajes_Eliminar(
    IN p_id BIGINT,
    IN p_user_id VARCHAR(100),
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_msg RECORD;
    v_is_admin BOOLEAN;
BEGIN
    SELECT m.id, m.user_id, m.conversacion_id, m.deleted_at
    INTO v_msg
    FROM mensajes m
    WHERE m.id = p_id;

    IF v_msg IS NULL OR v_msg.deleted_at IS NOT NULL THEN
        p_error_msg := 'MSG_006: Mensaje no encontrado';
        RETURN;
    END IF;

    v_is_admin := EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = v_msg.conversacion_id
          AND user_id = p_user_id
          AND rol = 'admin'
          AND left_at IS NULL
    );

    IF v_msg.user_id != p_user_id AND NOT v_is_admin THEN
        p_error_msg := 'AUTH_001: No tiene permisos para eliminar este mensaje';
        RETURN;
    END IF;

    UPDATE mensajes SET deleted_at = CURRENT_TIMESTAMP WHERE id = p_id;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
