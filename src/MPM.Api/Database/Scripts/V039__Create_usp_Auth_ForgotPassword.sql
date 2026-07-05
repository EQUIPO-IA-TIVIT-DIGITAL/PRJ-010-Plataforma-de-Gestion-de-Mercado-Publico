CREATE OR REPLACE PROCEDURE usp_Auth_ForgotPassword(
    IN p_email VARCHAR(255),
    IN p_expiration_hours INT DEFAULT 1,
    INOUT p_token VARCHAR(255) DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_token_id BIGINT;
BEGIN
    -- Validar email
    IF p_email IS NULL OR TRIM(p_email) = '' THEN
        p_error_msg := 'El email es requerido';
        RETURN;
    END IF;
    
    -- Invalidar tokens anteriores para este email
    UPDATE password_reset_tokens
    SET used_at = CURRENT_TIMESTAMP
    WHERE email = LOWER(TRIM(p_email))
      AND used_at IS NULL
      AND expires_at > CURRENT_TIMESTAMP;
    
    -- Generar nuevo token (UUID sin guiones para URL-friendly)
    p_token := REPLACE(gen_random_uuid()::TEXT, '-', '');
    
    -- Insertar nuevo token
    INSERT INTO password_reset_tokens (email, token, expires_at)
    VALUES (LOWER(TRIM(p_email)), p_token, CURRENT_TIMESTAMP + (p_expiration_hours || ' hours')::INTERVAL)
    RETURNING id INTO v_token_id;
    
EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
