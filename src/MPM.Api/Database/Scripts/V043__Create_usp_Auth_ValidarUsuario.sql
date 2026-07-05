-- Valida credenciales de usuario contra tabla usuarios
CREATE OR REPLACE PROCEDURE usp_Auth_ValidarUsuario(
    IN p_email VARCHAR(255),
    IN p_password VARCHAR(255),
    INOUT p_user_id BIGINT DEFAULT NULL,
    INOUT p_nombre VARCHAR(200) DEFAULT NULL,
    INOUT p_roles TEXT[] DEFAULT NULL,
    INOUT p_tenant_id VARCHAR(100) DEFAULT NULL,
    INOUT p_tenant_nombre VARCHAR(200) DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_user RECORD;
BEGIN
    -- Validar inputs
    IF p_email IS NULL OR TRIM(p_email) = '' THEN
        p_error_msg := 'El email es requerido';
        RETURN;
    END IF;
    
    IF p_password IS NULL OR p_password = '' THEN
        p_error_msg := 'La contraseña es requerida';
        RETURN;
    END IF;
    
    -- Buscar usuario activo
    SELECT id, nombre, password_hash, roles, tenant_id, tenant_nombre, activo
    INTO v_user
    FROM usuarios
    WHERE email = LOWER(TRIM(p_email))
      AND deleted_at IS NULL;
    
    -- Verificar que exista
    IF v_user.id IS NULL THEN
        p_error_msg := 'Credenciales inválidas';
        RETURN;
    END IF;
    
    -- Verificar que esté activo
    IF NOT v_user.activo THEN
        p_error_msg := 'Usuario desactivado';
        RETURN;
    END IF;
    
    -- Verificar contraseña con pgcrypto
    IF v_user.password_hash != crypt(p_password, v_user.password_hash) THEN
        p_error_msg := 'Credenciales inválidas';
        RETURN;
    END IF;
    
    -- Actualizar último login
    UPDATE usuarios SET ultimo_login = CURRENT_TIMESTAMP WHERE id = v_user.id;
    
    -- Retornar datos del usuario
    p_user_id := v_user.id;
    p_nombre := v_user.nombre;
    p_roles := v_user.roles;
    p_tenant_id := v_user.tenant_id;
    p_tenant_nombre := v_user.tenant_nombre;
    
EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
