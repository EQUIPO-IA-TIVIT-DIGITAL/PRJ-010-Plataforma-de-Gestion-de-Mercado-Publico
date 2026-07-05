CREATE OR REPLACE PROCEDURE usp_Conversaciones_Actualizar(
    IN p_id BIGINT,
    IN p_asunto VARCHAR(200),
    IN p_user_id VARCHAR(100),
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM conversaciones WHERE id = p_id AND deleted_at IS NULL) THEN
        p_error_msg := 'MSG_001: Conversacion no encontrada';
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = p_id AND user_id = p_user_id AND rol = 'admin' AND left_at IS NULL
    ) THEN
        p_error_msg := 'AUTH_001: No tiene permisos para realizar esta accion';
        RETURN;
    END IF;

    IF EXISTS (SELECT 1 FROM conversaciones WHERE id = p_id AND tipo = 'directo') THEN
        p_error_msg := 'MSG_003: No se puede actualizar el asunto de una conversacion directa';
        RETURN;
    END IF;

    UPDATE conversaciones
    SET asunto = p_asunto, updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
