-- V131: SPs del módulo de administración de usuarios (Admin/SuperAdmin).
-- Modelo de roles jerárquico: SuperAdmin > Admin > Analista/Usuario.
-- - Un Admin puede crear/gestionar Analista y Usuario.
-- - Solo un SuperAdmin puede crear/gestionar Admins y SuperAdmins.
-- - La jerarquía se valida en la capa de servicio (AdminRoleRules) y acá solo se
--   validan reglas de integridad de datos (email, password, rol permitido, unicidad).
-- - Alta de usuario con contraseña temporal hasheada (bcrypt, factor 11); el usuario
--   la cambia en su primer ingreso (PUT /api/v1/usuarios/mi-password).

-- ── Crear usuario ────────────────────────────────────────────────────────────

CREATE OR REPLACE PROCEDURE usp_Admin_CrearUsuario(
    IN p_email VARCHAR(255),
    IN p_nombre VARCHAR(200),
    IN p_password VARCHAR(255),
    IN p_rol VARCHAR(50),
    IN p_tenant_id VARCHAR(100),
    IN p_tenant_nombre VARCHAR(200),
    INOUT p_user_id BIGINT DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Validar inputs
    IF p_email IS NULL OR TRIM(p_email) = '' THEN
        p_error_msg := 'El email es requerido';
        RETURN;
    END IF;

    IF p_email !~ '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$' THEN
        p_error_msg := 'El email no tiene un formato válido';
        RETURN;
    END IF;

    IF p_nombre IS NULL OR TRIM(p_nombre) = '' THEN
        p_error_msg := 'El nombre es requerido';
        RETURN;
    END IF;

    IF p_password IS NULL OR LENGTH(p_password) < 6 THEN
        p_error_msg := 'La contraseña debe tener al menos 6 caracteres';
        RETURN;
    END IF;

    IF p_rol IS NULL OR p_rol NOT IN ('SuperAdmin', 'Admin', 'Analista', 'Usuario') THEN
        p_error_msg := 'El rol no es válido';
        RETURN;
    END IF;

    -- Unicidad de email (incluye borrados lógicos: no se puede reutilizar)
    IF EXISTS (SELECT 1 FROM usuarios WHERE email = LOWER(TRIM(p_email))) THEN
        p_error_msg := 'Ya existe un usuario con ese correo';
        RETURN;
    END IF;

    INSERT INTO usuarios (email, nombre, password_hash, roles, tenant_id, tenant_nombre, activo)
    VALUES (
        LOWER(TRIM(p_email)),
        TRIM(p_nombre),
        crypt(p_password, gen_salt('bf', 11)),
        ARRAY[p_rol],
        p_tenant_id,
        p_tenant_nombre,
        TRUE
    )
    RETURNING id INTO p_user_id;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;

-- ── Listar usuarios (paginado) ───────────────────────────────────────────────

CREATE OR REPLACE FUNCTION usp_Admin_ListarUsuarios(
    p_search VARCHAR(200) DEFAULT NULL,
    p_pagina INT DEFAULT 1,
    p_pagina_size INT DEFAULT 20
)
RETURNS TABLE(
    p_id BIGINT,
    p_email VARCHAR(255),
    p_nombre VARCHAR(200),
    p_roles TEXT[],
    p_activo BOOLEAN,
    p_ultimo_login TIMESTAMP,
    p_tenant_nombre VARCHAR(200),
    p_es_account_manager BOOLEAN,
    p_total_count BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        u.id,
        u.email,
        u.nombre,
        u.roles,
        u.activo,
        u.ultimo_login,
        u.tenant_nombre,
        EXISTS (
            SELECT 1 FROM alertas_destinatarios d
            WHERE d.usuario_id = u.id::VARCHAR AND d.es_account_manager_gobierno
        ) AS es_account_manager,
        COUNT(*) OVER() AS total_count
    FROM usuarios u
    WHERE u.deleted_at IS NULL
      AND (p_search IS NULL OR TRIM(p_search) = ''
           OR u.nombre ILIKE '%' || p_search || '%'
           OR u.email ILIKE '%' || p_search || '%')
    ORDER BY u.nombre
    LIMIT p_pagina_size OFFSET (p_pagina - 1) * p_pagina_size;
END;
$$ LANGUAGE plpgsql;

-- ── Activar / desactivar usuario ─────────────────────────────────────────────

CREATE OR REPLACE PROCEDURE usp_Admin_ActualizarEstado(
    IN p_user_id BIGINT,
    IN p_activo BOOLEAN,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_user_id AND deleted_at IS NULL) THEN
        p_error_msg := 'El usuario no existe';
        RETURN;
    END IF;

    UPDATE usuarios
    SET activo = p_activo, updated_at = CURRENT_TIMESTAMP
    WHERE id = p_user_id;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;

-- ── Cambiar rol de usuario ───────────────────────────────────────────────────

CREATE OR REPLACE PROCEDURE usp_Admin_ActualizarRol(
    IN p_user_id BIGINT,
    IN p_rol VARCHAR(50),
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_rol IS NULL OR p_rol NOT IN ('SuperAdmin', 'Admin', 'Analista', 'Usuario') THEN
        p_error_msg := 'El rol no es válido';
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_user_id AND deleted_at IS NULL) THEN
        p_error_msg := 'El usuario no existe';
        RETURN;
    END IF;

    UPDATE usuarios
    SET roles = ARRAY[p_rol], updated_at = CURRENT_TIMESTAMP
    WHERE id = p_user_id;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;

-- ── Marcar/desmarcar account manager de gobierno ────────────────────────────
-- El flag vive en alertas_destinatarios (destinos de entrega de alertas por
-- usuario). Si el usuario aún no tiene fila, se crea solo con el flag.

CREATE OR REPLACE PROCEDURE usp_Admin_SetAccountManager(
    IN p_usuario_id BIGINT,
    IN p_es_account_manager BOOLEAN,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuarios WHERE id = p_usuario_id AND deleted_at IS NULL) THEN
        p_error_msg := 'El usuario no existe';
        RETURN;
    END IF;

    INSERT INTO alertas_destinatarios (usuario_id, es_account_manager_gobierno)
    VALUES (p_usuario_id::VARCHAR, p_es_account_manager)
    ON CONFLICT (usuario_id)
    DO UPDATE SET es_account_manager_gobierno = EXCLUDED.es_account_manager_gobierno,
                  updated_at = CURRENT_TIMESTAMP;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
