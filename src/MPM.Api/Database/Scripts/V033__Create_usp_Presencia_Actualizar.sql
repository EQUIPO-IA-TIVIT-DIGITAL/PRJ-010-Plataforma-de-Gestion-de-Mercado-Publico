CREATE OR REPLACE PROCEDURE usp_Presencia_Actualizar(
    IN p_user_id VARCHAR(100),
    IN p_estado VARCHAR(15),
    IN p_conversacion_id BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO usuario_presencia (user_id, estado, conversacion_id, updated_at)
    VALUES (p_user_id, p_estado, p_conversacion_id, CURRENT_TIMESTAMP)
    ON CONFLICT (user_id)
    DO UPDATE SET
        estado = p_estado,
        conversacion_id = p_conversacion_id,
        updated_at = CURRENT_TIMESTAMP;
END;
$$;
