-- V117: las notificaciones "broadcast" (usuario_id = '00000000-...', emitidas por el
-- scraper/sync) hoy las devuelve usp_Notificaciones_Listar/ContarNoLeidas a CUALQUIER
-- usuario autenticado -- la restriccion a "solo admin" vivia unicamente en el frontend
-- (NotificacionesPage.tsx, isAdmin), que cualquiera podia saltarse llamando la API
-- directo. Se mueve la regla al backend: el broadcast solo se incluye si el usuario que
-- pide la lista es admin@tivit.cl (chequeo exacto de email, no por rol SuperAdmin -- un
-- SuperAdmin sembrado como cuenta de un usuario real, ej. Francisco Lopez via V115/V116,
-- ya NO ve estas notificaciones aunque tenga ese rol).
--
-- De paso: usp_Notificaciones_Eliminar/EliminarTodas (V077) tampoco chequeaban admin --
-- cualquier usuario podia borrar la fila broadcast compartida al usar "Eliminar todas"
-- en su propia bandeja, dejando a admin@tivit.cl sin su historial de notificaciones del
-- scraper. Se corrige con el mismo chequeo.

DROP FUNCTION IF EXISTS usp_Notificaciones_Listar(text,integer,integer,boolean);

CREATE FUNCTION usp_Notificaciones_Listar(
    p_usuario_id    TEXT,
    p_page          INT DEFAULT 1,
    p_page_size     INT DEFAULT 20,
    p_solo_no_leidas BOOLEAN DEFAULT FALSE
)
RETURNS TABLE (
    id          BIGINT,
    usuario_id  TEXT,
    tipo        VARCHAR(50),
    titulo      TEXT,
    mensaje     TEXT,
    metadata    JSONB,
    leido       BOOLEAN,
    created_at  TIMESTAMP,
    total_count BIGINT
) AS $func$
DECLARE
    v_es_admin BOOLEAN;
BEGIN
    SELECT (u.email = 'admin@tivit.cl') INTO v_es_admin
    FROM usuarios u
    WHERE u.id::text = p_usuario_id AND u.deleted_at IS NULL;

    v_es_admin := COALESCE(v_es_admin, FALSE);

    RETURN QUERY
    SELECT
        n.id,
        n.usuario_id,
        n.tipo,
        n.titulo,
        n.mensaje,
        n.metadata,
        n.leido,
        n.created_at,
        COUNT(*) OVER() AS total_count
    FROM notificaciones n
    WHERE (n.usuario_id = p_usuario_id OR (v_es_admin AND n.usuario_id = '00000000-0000-0000-0000-000000000000'))
      AND n.record_status = 1
      AND (NOT p_solo_no_leidas OR n.leido = FALSE)
    ORDER BY n.created_at DESC
    LIMIT p_page_size
    OFFSET (p_page - 1) * p_page_size;
END;
$func$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Notificaciones_ContarNoLeidas(
    p_usuario_id TEXT,
    OUT p_count BIGINT
)
RETURNS BIGINT AS $func$
DECLARE
    v_es_admin BOOLEAN;
BEGIN
    SELECT (u.email = 'admin@tivit.cl') INTO v_es_admin
    FROM usuarios u
    WHERE u.id::text = p_usuario_id AND u.deleted_at IS NULL;

    v_es_admin := COALESCE(v_es_admin, FALSE);

    SELECT COUNT(*) INTO p_count
    FROM notificaciones
    WHERE (usuario_id = p_usuario_id OR (v_es_admin AND usuario_id = '00000000-0000-0000-0000-000000000000'))
      AND leido = FALSE
      AND record_status = 1;
END;
$func$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Notificaciones_Eliminar(
    p_id BIGINT,
    p_usuario_id TEXT,
    OUT p_error_msg TEXT
) AS $$
DECLARE
    v_es_admin BOOLEAN;
BEGIN
    p_error_msg := NULL;

    SELECT (u.email = 'admin@tivit.cl') INTO v_es_admin
    FROM usuarios u
    WHERE u.id::text = p_usuario_id AND u.deleted_at IS NULL;

    v_es_admin := COALESCE(v_es_admin, FALSE);

    UPDATE notificaciones
    SET record_status = 0
    WHERE id = p_id
      AND (usuario_id = p_usuario_id OR (v_es_admin AND usuario_id = '00000000-0000-0000-0000-000000000000'))
      AND record_status = 1;

    IF NOT FOUND THEN
        p_error_msg := 'NOT_001: Notificacion no encontrada';
    END IF;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Notificaciones_EliminarTodas(
    p_usuario_id TEXT,
    OUT p_count INT,
    OUT p_error_msg TEXT
) AS $$
DECLARE
    v_es_admin BOOLEAN;
BEGIN
    p_error_msg := NULL;

    SELECT (u.email = 'admin@tivit.cl') INTO v_es_admin
    FROM usuarios u
    WHERE u.id::text = p_usuario_id AND u.deleted_at IS NULL;

    v_es_admin := COALESCE(v_es_admin, FALSE);

    UPDATE notificaciones
    SET record_status = 0
    WHERE (usuario_id = p_usuario_id OR (v_es_admin AND usuario_id = '00000000-0000-0000-0000-000000000000'))
      AND record_status = 1;

    GET DIAGNOSTICS p_count = ROW_COUNT;

EXCEPTION
    WHEN OTHERS THEN
        p_count := 0;
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$ LANGUAGE plpgsql;
