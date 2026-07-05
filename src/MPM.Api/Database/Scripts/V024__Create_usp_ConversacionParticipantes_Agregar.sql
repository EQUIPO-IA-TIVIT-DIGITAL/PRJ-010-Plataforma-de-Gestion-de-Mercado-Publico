CREATE OR REPLACE PROCEDURE usp_ConversacionParticipantes_Agregar(
    IN p_conversacion_id BIGINT,
    IN p_user_id VARCHAR(100),
    IN p_rol VARCHAR(10),
    IN p_solicitante_id VARCHAR(100),
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM conversaciones WHERE id = p_conversacion_id AND deleted_at IS NULL) THEN
        p_error_msg := 'MSG_001: Conversacion no encontrada';
        RETURN;
    END IF;

    IF EXISTS (SELECT 1 FROM conversaciones WHERE id = p_conversacion_id AND tipo = 'directo') THEN
        p_error_msg := 'MSG_003: No se pueden agregar participantes a una conversacion directa';
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = p_conversacion_id AND user_id = p_solicitante_id AND rol = 'admin' AND left_at IS NULL
    ) THEN
        p_error_msg := 'AUTH_001: No tiene permisos para realizar esta accion';
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = p_conversacion_id AND user_id = p_user_id AND left_at IS NULL
    ) THEN
        p_error_msg := 'MSG_002: El usuario ya es participante activo';
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = p_conversacion_id AND user_id = p_user_id AND left_at IS NOT NULL
    ) THEN
        UPDATE conversacion_participantes
        SET left_at = NULL, rol = p_rol, joined_at = CURRENT_TIMESTAMP
        WHERE conversacion_id = p_conversacion_id AND user_id = p_user_id AND left_at IS NOT NULL;
    ELSE
        INSERT INTO conversacion_participantes (conversacion_id, user_id, rol)
        VALUES (p_conversacion_id, p_user_id, p_rol);
    END IF;

    UPDATE conversaciones SET updated_at = CURRENT_TIMESTAMP WHERE id = p_conversacion_id;

    INSERT INTO mensajes (conversacion_id, user_id, tipo, contenido)
    VALUES (p_conversacion_id, 'system', 'sistema', p_user_id || ' se unio a la conversacion');

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
