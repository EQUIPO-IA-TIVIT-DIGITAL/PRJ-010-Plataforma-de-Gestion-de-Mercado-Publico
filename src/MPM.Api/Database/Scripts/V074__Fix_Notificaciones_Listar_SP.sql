-- V074: Fix usp_Notificaciones_Listar to use RETURNS TABLE (compatible with Dapper)
-- and include system-broadcast notifications (usuario_id = '00000000-...' sent by background services)

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
BEGIN
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
    WHERE (n.usuario_id = p_usuario_id OR n.usuario_id = '00000000-0000-0000-0000-000000000000')
      AND n.record_status = 1
      AND (NOT p_solo_no_leidas OR n.leido = FALSE)
    ORDER BY n.created_at DESC
    LIMIT p_page_size
    OFFSET (p_page - 1) * p_page_size;
END;
$func$ LANGUAGE plpgsql;

-- Also fix ContarNoLeidas to include system notifications
CREATE OR REPLACE FUNCTION usp_Notificaciones_ContarNoLeidas(
    p_usuario_id TEXT,
    OUT p_count BIGINT
)
RETURNS BIGINT AS $func$
BEGIN
    SELECT COUNT(*) INTO p_count
    FROM notificaciones
    WHERE (usuario_id = p_usuario_id OR usuario_id = '00000000-0000-0000-0000-000000000000')
      AND leido = FALSE
      AND record_status = 1;
END;
$func$ LANGUAGE plpgsql;
