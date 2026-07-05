CREATE OR REPLACE FUNCTION usp_Conversaciones_Listar(
    p_user_id VARCHAR(100),
    p_page INT DEFAULT 1,
    p_page_size INT DEFAULT 20,
    p_search VARCHAR(200) DEFAULT NULL,
    p_sort_by VARCHAR(50) DEFAULT 'updated_at',
    p_sort_dir VARCHAR(4) DEFAULT 'desc'
)
RETURNS TABLE (
    Id BIGINT,
    Tipo VARCHAR(10),
    Asunto VARCHAR(200),
    LicitacionId BIGINT,
    LicitacionNombre VARCHAR(500),
    Participantes JSONB,
    UltimoMensaje JSONB,
    NoLeidos BIGINT,
    UpdatedAt TIMESTAMP,
    TotalCount BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_offset INT;
    v_sort_column VARCHAR(50);
    v_sort_direction VARCHAR(4);
BEGIN
    v_offset := (p_page - 1) * p_page_size;

    v_sort_column := CASE
        WHEN p_sort_by IN ('updated_at', 'created_at') THEN p_sort_by
        ELSE 'updated_at'
    END;

    v_sort_direction := CASE
        WHEN UPPER(p_sort_dir) = 'ASC' THEN 'ASC'
        ELSE 'DESC'
    END;

    RETURN QUERY
    WITH user_convs AS (
        SELECT c.id, c.tipo, c.asunto, c.licitacion_id, c.updated_at
        FROM conversaciones c
        INNER JOIN conversacion_participantes cp ON cp.conversacion_id = c.id
        WHERE cp.user_id = p_user_id
          AND cp.left_at IS NULL
          AND c.deleted_at IS NULL
          AND (p_search IS NULL OR c.asunto ILIKE '%' || p_search || '%')
    ),
    total AS (
        SELECT COUNT(*) AS cnt FROM user_convs
    ),
    paged AS (
        SELECT uc.*
        FROM user_convs uc
        ORDER BY
            CASE WHEN v_sort_column = 'updated_at' AND v_sort_direction = 'DESC' THEN uc.updated_at END DESC,
            CASE WHEN v_sort_column = 'updated_at' AND v_sort_direction = 'ASC' THEN uc.updated_at END ASC,
            CASE WHEN v_sort_column = 'created_at' AND v_sort_direction = 'DESC' THEN uc.updated_at END DESC,
            CASE WHEN v_sort_column = 'created_at' AND v_sort_direction = 'ASC' THEN uc.updated_at END ASC
        LIMIT p_page_size OFFSET v_offset
    ),
    participantes_agg AS (
        SELECT
            cp.conversacion_id,
            jsonb_agg(jsonb_build_object(
                'userId', cp.user_id,
                'nombre', cp.user_id,
                'rol', cp.rol
            )) AS participantes
        FROM conversacion_participantes cp
        WHERE cp.conversacion_id IN (SELECT id FROM paged)
          AND cp.left_at IS NULL
        GROUP BY cp.conversacion_id
    ),
    ultimo_msg AS (
        SELECT DISTINCT ON (m.conversacion_id)
            m.conversacion_id,
            jsonb_build_object(
                'id', m.id,
                'userId', m.user_id,
                'tipo', m.tipo,
                'contenido', LEFT(m.contenido, 100),
                'createdAt', m.created_at
            ) AS ultimo_mensaje
        FROM mensajes m
        WHERE m.conversacion_id IN (SELECT id FROM paged)
          AND m.deleted_at IS NULL
        ORDER BY m.conversacion_id, m.created_at DESC
    ),
    no_leidos AS (
        SELECT
            m.conversacion_id,
            COUNT(*) AS count
        FROM mensajes m
        WHERE m.conversacion_id IN (SELECT id FROM paged)
          AND m.deleted_at IS NULL
          AND m.user_id != p_user_id
          AND NOT EXISTS (
              SELECT 1 FROM mensaje_estados me
              WHERE me.mensaje_id = m.id
                AND me.user_id = p_user_id
                AND me.estado = 'leido'
          )
        GROUP BY m.conversacion_id
    )
    SELECT
        p.id,
        p.tipo,
        p.asunto,
        p.licitacion_id,
        l.nombre AS LicitacionNombre,
        COALESCE(pa.participantes, '[]'::jsonb),
        um.ultimo_mensaje,
        COALESCE(nl.count, 0),
        p.updated_at,
        t.cnt
    FROM paged p
    LEFT JOIN licitaciones l ON l.id = p.licitacion_id
    LEFT JOIN participantes_agg pa ON pa.conversacion_id = p.id
    LEFT JOIN ultimo_msg um ON um.conversacion_id = p.id
    LEFT JOIN no_leidos nl ON nl.conversacion_id = p.id
    CROSS JOIN total t
    ORDER BY
        CASE WHEN v_sort_column = 'updated_at' AND v_sort_direction = 'DESC' THEN p.updated_at END DESC,
        CASE WHEN v_sort_column = 'updated_at' AND v_sort_direction = 'ASC' THEN p.updated_at END ASC;
END;
$$;
