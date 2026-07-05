CREATE OR REPLACE PROCEDURE usp_Auth_ResetPassword(
    IN p_token VARCHAR(255),
    IN p_new_password VARCHAR(255),
    INOUT p_email VARCHAR(255) DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_token_record RECORD;
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
    
    -- Marcar token como usado
    UPDATE password_reset_tokens
    SET used_at = CURRENT_TIMESTAMP
    WHERE id = v_token_record.id;
    
    -- Retornar email para que la API pueda actualizar la contraseña del usuario
    -- En una implementación real con tabla de usuarios, aquí se actualizaría la contraseña
    p_email := v_token_record.email;
    
    -- NOTA: En producción, aquí debería actualizarse la contraseña en la tabla de usuarios
    -- UPDATE usuarios SET password_hash = hash(p_new_password) WHERE email = p_email;
    
EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
