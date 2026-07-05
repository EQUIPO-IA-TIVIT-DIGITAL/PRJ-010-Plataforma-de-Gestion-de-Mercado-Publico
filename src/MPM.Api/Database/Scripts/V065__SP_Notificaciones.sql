-- V065: Stored procedures para modulo de notificaciones

CREATE OR REPLACE FUNCTION usp_Notificaciones_Crear(
    p_usuario_id TEXT,
    p_tipo VARCHAR(50),
    p_titulo TEXT,
    p_mensaje TEXT,
    p_metadata JSONB DEFAULT NULL,
    OUT p_id BIGINT,
    OUT p_error_msg TEXT
) AS $$
BEGIN
    p_error_msg := NULL;

    INSERT INTO notificaciones (usuario_id, tipo, titulo, mensaje, metadata)
    VALUES (p_usuario_id, p_tipo, p_titulo, p_mensaje, p_metadata)
    RETURNING id INTO p_id;

EXCEPTION
    WHEN OTHERS THEN
        p_id := 0;
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_Notificaciones_Listar(
    p_usuario_id TEXT,
    p_page INT DEFAULT 1,
    p_page_size INT DEFAULT 20,
    p_solo_no_leidas BOOLEAN DEFAULT FALSE,
    OUT p_ref REFCURSOR,
    OUT p_total_count BIGINT
) AS $$
BEGIN
    p_total_count := 0;

    IF p_solo_no_leidas THEN
        SELECT COUNT(*) INTO p_total_count
        FROM notificaciones
        WHERE usuario_id = p_usuario_id
          AND leido = FALSE
          AND record_status = 1;
    ELSE
        SELECT COUNT(*) INTO p_total_count
        FROM notificaciones
        WHERE usuario_id = p_usuario_id
          AND record_status = 1;
    END IF;

    OPEN p_ref FOR
        SELECT id, usuario_id, tipo, titulo, mensaje, metadata, leido, created_at
        FROM notificaciones
        WHERE usuario_id = p_usuario_id
          AND record_status = 1
          AND (NOT p_solo_no_leidas OR leido = FALSE)
        ORDER BY created_at DESC
        LIMIT p_page_size
        OFFSET (p_page - 1) * p_page_size;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_Notificaciones_ContarNoLeidas(
    p_usuario_id TEXT,
    OUT p_count BIGINT
) AS $$
BEGIN
    SELECT COUNT(*) INTO p_count
    FROM notificaciones
    WHERE usuario_id = p_usuario_id
      AND leido = FALSE
      AND record_status = 1;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_Notificaciones_MarcarLeida(
    p_id BIGINT,
    p_usuario_id TEXT,
    OUT p_error_msg TEXT
) AS $$
BEGIN
    p_error_msg := NULL;

    UPDATE notificaciones
    SET leido = TRUE
    WHERE id = p_id
      AND usuario_id = p_usuario_id
      AND record_status = 1;

    IF NOT FOUND THEN
        p_error_msg := 'NOT_001: Notificacion no encontrada';
    END IF;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_Notificaciones_MarcarTodasLeidas(
    p_usuario_id TEXT,
    OUT p_count INT,
    OUT p_error_msg TEXT
) AS $$
BEGIN
    p_error_msg := NULL;

    UPDATE notificaciones
    SET leido = TRUE
    WHERE usuario_id = p_usuario_id
      AND leido = FALSE
      AND record_status = 1;

    GET DIAGNOSTICS p_count = ROW_COUNT;

EXCEPTION
    WHEN OTHERS THEN
        p_count := 0;
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$ LANGUAGE plpgsql;
