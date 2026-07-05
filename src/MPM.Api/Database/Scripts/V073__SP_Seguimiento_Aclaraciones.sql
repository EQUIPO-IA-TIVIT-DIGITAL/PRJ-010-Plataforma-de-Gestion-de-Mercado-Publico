-- V073: Stored procedures para seguimiento de licitaciones y detección de aclaraciones

-- Toggle seguir/dejar de seguir una licitación
CREATE OR REPLACE FUNCTION usp_Licitaciones_SeguirToggle(
    p_usuario_id   TEXT,
    p_codigo       VARCHAR(50)
)
RETURNS TABLE (p_accion TEXT, p_error_msg TEXT) AS $$
DECLARE
    v_existe BOOLEAN;
BEGIN
    SELECT EXISTS(
        SELECT 1 FROM licitaciones_seguidas
        WHERE usuario_id = p_usuario_id AND codigo_externo = p_codigo
    ) INTO v_existe;

    IF v_existe THEN
        DELETE FROM licitaciones_seguidas
        WHERE usuario_id = p_usuario_id AND codigo_externo = p_codigo;
        RETURN QUERY SELECT 'no_seguida'::TEXT, NULL::TEXT;
    ELSE
        INSERT INTO licitaciones_seguidas (usuario_id, codigo_externo)
        VALUES (p_usuario_id, p_codigo)
        ON CONFLICT (usuario_id, codigo_externo) DO NOTHING;
        RETURN QUERY SELECT 'seguida'::TEXT, NULL::TEXT;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RETURN QUERY SELECT NULL::TEXT, SQLERRM::TEXT;
END;
$$ LANGUAGE plpgsql;

-- Verificar si un usuario sigue una licitación
CREATE OR REPLACE FUNCTION usp_Licitaciones_EsSeguida(
    p_usuario_id   TEXT,
    p_codigo       VARCHAR(50)
)
RETURNS TABLE (p_es_seguida BOOLEAN) AS $$
BEGIN
    RETURN QUERY
    SELECT EXISTS(
        SELECT 1 FROM licitaciones_seguidas
        WHERE usuario_id = p_usuario_id AND codigo_externo = p_codigo
    );
END;
$$ LANGUAGE plpgsql;

-- Obtener todas las licitaciones seguidas activas para el monitor
-- Retorna una fila por licitación con array de usuarios que la siguen
CREATE OR REPLACE FUNCTION usp_Licitaciones_ObtenerParaMonitor(
    p_estados INT[]
)
RETURNS TABLE (
    codigo_externo  VARCHAR(50),
    nombre          VARCHAR(500),
    codigo_estado   SMALLINT,
    usuario_ids     TEXT[]
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        l.codigo_externo,
        l.nombre,
        l.codigo_estado,
        ARRAY_AGG(DISTINCT ls.usuario_id) AS usuario_ids
    FROM licitaciones_seguidas ls
    JOIN licitaciones l ON l.codigo_externo = ls.codigo_externo
    WHERE l.codigo_estado = ANY(p_estados::SMALLINT[])
      AND l.deleted_at IS NULL
    GROUP BY l.codigo_externo, l.nombre, l.codigo_estado;
END;
$$ LANGUAGE plpgsql;

-- Upsert de una aclaración detectada; retorna si es nueva
CREATE OR REPLACE FUNCTION usp_Licitaciones_Aclaracion_Upsert(
    p_codigo              VARCHAR(50),
    p_codigo_aclaracion   INT,
    p_pregunta            TEXT,
    p_respuesta           TEXT,
    p_fecha_publicacion   TIMESTAMP,
    p_fecha_respuesta     TIMESTAMP
)
RETURNS TABLE (p_es_nueva BOOLEAN, p_id BIGINT) AS $$
DECLARE
    v_id     BIGINT;
    v_es_nueva BOOLEAN := FALSE;
BEGIN
    INSERT INTO licitaciones_aclaraciones
        (codigo_externo, codigo_aclaracion, pregunta, respuesta, fecha_publicacion, fecha_respuesta)
    VALUES
        (p_codigo, p_codigo_aclaracion, p_pregunta, p_respuesta, p_fecha_publicacion, p_fecha_respuesta)
    ON CONFLICT (codigo_externo, codigo_aclaracion) DO NOTHING
    RETURNING id INTO v_id;

    IF v_id IS NOT NULL THEN
        v_es_nueva := TRUE;
    ELSE
        SELECT id INTO v_id
        FROM licitaciones_aclaraciones
        WHERE codigo_externo = p_codigo AND codigo_aclaracion = p_codigo_aclaracion;
    END IF;

    RETURN QUERY SELECT v_es_nueva, v_id;
END;
$$ LANGUAGE plpgsql;

-- Marcar aclaración como notificada
CREATE OR REPLACE FUNCTION usp_Licitaciones_Aclaracion_MarcarNotificada(
    p_id BIGINT
)
RETURNS VOID AS $$
BEGIN
    UPDATE licitaciones_aclaraciones
    SET notificado = TRUE
    WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

-- Listar licitaciones que sigue un usuario
CREATE OR REPLACE FUNCTION usp_Licitaciones_ObtenerSeguidas(
    p_usuario_id TEXT
)
RETURNS TABLE (
    codigo_externo    VARCHAR(50),
    nombre            VARCHAR(500),
    codigo_estado     SMALLINT,
    fecha_publicacion TIMESTAMP,
    fecha_cierre      TIMESTAMP,
    seguida_desde     TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        l.codigo_externo,
        l.nombre,
        l.codigo_estado,
        l.fecha_publicacion,
        l.fecha_cierre,
        ls.created_at AS seguida_desde
    FROM licitaciones_seguidas ls
    JOIN licitaciones l ON l.codigo_externo = ls.codigo_externo
    WHERE ls.usuario_id = p_usuario_id
      AND l.deleted_at IS NULL
    ORDER BY ls.created_at DESC;
END;
$$ LANGUAGE plpgsql;
