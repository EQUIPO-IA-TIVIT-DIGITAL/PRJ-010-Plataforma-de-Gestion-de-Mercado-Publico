CREATE OR REPLACE PROCEDURE usp_Mensajes_MarcarLeido(
    IN p_mensaje_id BIGINT,
    IN p_user_id VARCHAR(100)
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO mensaje_estados (mensaje_id, user_id, estado, updated_at)
    VALUES (p_mensaje_id, p_user_id, 'leido', CURRENT_TIMESTAMP)
    ON CONFLICT (mensaje_id, user_id)
    DO UPDATE SET estado = 'leido', updated_at = CURRENT_TIMESTAMP;
END;
$$;
