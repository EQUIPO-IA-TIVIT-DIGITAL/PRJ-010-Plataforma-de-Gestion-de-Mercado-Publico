-- Actualiza la contraseña de un usuario usando un token de recuperación válido
CREATE OR REPLACE PROCEDURE usp_Auth_ActualizarPassword(
    IN p_token VARCHAR(255),
    IN p_new_password VARCHAR(255),
    INOUT p_email VARCHAR(255) DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_token_record RECORD;
    v_user_id BIGINT;
BEGIN
    -- Validar inputs
    IF p_token IS NULL OR TRIM(p_token) = '' THEN
        p_error_msg := 'El token es requerido';
        RETURN;
    END IF;
    
    IF p_new_password IS NULL OR LENGTH(p_new_password) < 6 THEN
        p_error_msg := 'La contraseña debe tener al menos 6 caracteres';
        RETURN;
    END IF;
    
    -- Buscar token válido
    SELECT id, email, expires_at, used_at
    INTO v_token_record
    FROM password_reset_tokens
    WHERE token = p_token;
    
    -- Validar que el token exista
    IF v_token_record.id IS NULL THEN
        p_error_msg := 'Token inválido o no encontrado';
        RETURN;
    END IF;
    
    -- Validar que el token no haya sido usado
    IF v_token_record.used_at IS NOT NULL THEN
        p_error_msg := 'Este token ya ha sido utilizado';
        RETURN;
    END IF;
    
    -- Validar que el token no haya expirado
    IF v_token_record.expires_at < CURRENT_TIMESTAMP THEN
        p_error_msg := 'El token ha expirado';
        RETURN;
    END IF;
    
    -- Buscar usuario por email
    SELECT id INTO v_user_id
    FROM usuarios
    WHERE email = v_token_record.email
      AND deleted_at IS NULL;
    
    IF v_user_id IS NULL THEN
        p_error_msg := 'Usuario no encontrado';
        RETURN;
    END IF;
    
    -- Marcar token como usado
    UPDATE password_reset_tokens
    SET used_at = CURRENT_TIMESTAMP
    WHERE id = v_token_record.id;
    
    -- Actualizar contraseña hasheada del usuario
    UPDATE usuarios
    SET password_hash = crypt(p_new_password, gen_salt('bf', 11)),
        updated_at = CURRENT_TIMESTAMP
    WHERE id = v_user_id;
    
    -- Retornar email
    p_email := v_token_record.email;
    
EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
