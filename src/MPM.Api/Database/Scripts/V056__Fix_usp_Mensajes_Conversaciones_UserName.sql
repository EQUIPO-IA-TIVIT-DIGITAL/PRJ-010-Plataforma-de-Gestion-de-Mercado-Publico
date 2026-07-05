-- Fix: usp_Mensajes_Listar - resolver nombre real del usuario
CREATE OR REPLACE FUNCTION usp_Mensajes_Listar(
    p_conversacion_id BIGINT,
    p_user_id VARCHAR(100),
    p_page INT DEFAULT 1,
    p_page_size INT DEFAULT 50,
    p_before BIGINT DEFAULT NULL
)
RETURNS TABLE (
    Id BIGINT,
    UserId VARCHAR(100),
    UserName VARCHAR(100),
    Tipo VARCHAR(10),
    Contenido TEXT,
    ReplyToId BIGINT,
    ReplyToContenido TEXT,
    Adjuntos JSONB,
    Estados JSONB,
    EditedAt TIMESTAMP,
    CreatedAt TIMESTAMP,
    TotalCount BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_offset INT;
BEGIN
    v_offset := (p_page - 1) * p_page_size;

    RETURN QUERY
    WITH filtered AS (
        SELECT m.*
        FROM mensajes m
        WHERE m.conversacion_id = p_conversacion_id
          AND m.deleted_at IS NULL
          AND (p_before IS NULL OR m.id < p_before)
        ORDER BY m.created_at DESC
    ),
    total AS (
        SELECT COUNT(*) AS cnt FROM filtered
    ),
    paged AS (
        SELECT f.* FROM filtered f
        LIMIT p_page_size OFFSET v_offset
    ),
    adjuntos_agg AS (
        SELECT
            ma.mensaje_id,
            jsonb_agg(jsonb_build_object(
                'id', ma.id,
                'nombreArchivo', ma.nombre_archivo,
                'mimeType', ma.mime_type,
                'tamanioBytes', ma.tamanio_bytes,
                'downloadUrl', '/api/v1/conversaciones/' || p_conversacion_id || '/mensajes/' || ma.mensaje_id || '/adjuntos/' || ma.id,
                'createdAt', ma.created_at
            )) AS adjuntos
        FROM mensaje_adjuntos ma
        WHERE ma.mensaje_id IN (SELECT paged.id FROM paged)
        GROUP BY ma.mensaje_id
    ),
    estados_agg AS (
        SELECT
            me.mensaje_id,
            jsonb_agg(jsonb_build_object(
                'userId', me.user_id,
                'estado', me.estado,
                'updatedAt', me.updated_at
            )) AS estados
        FROM mensaje_estados me
        WHERE me.mensaje_id IN (SELECT paged.id FROM paged)
        GROUP BY me.mensaje_id
    )
    SELECT
        p.id,
        p.user_id,
        COALESCE(u.nombre, p.user_id),
        p.tipo,
        p.contenido,
        p.reply_to_id,
        rm.contenido,
        COALESCE(aa.adjuntos, '[]'::jsonb),
        COALESCE(ea.estados, '[]'::jsonb),
        p.edited_at,
        p.created_at,
        t.cnt
    FROM paged p
    LEFT JOIN usuarios u ON u.id::text = p.user_id AND u.deleted_at IS NULL
    LEFT JOIN mensajes rm ON rm.id = p.reply_to_id AND rm.deleted_at IS NULL
    LEFT JOIN adjuntos_agg aa ON aa.mensaje_id = p.id
    LEFT JOIN estados_agg ea ON ea.mensaje_id = p.id
    CROSS JOIN total t
    ORDER BY p.created_at ASC;
END;
$$;

-- Fix: usp_Conversaciones_Listar - resolver nombre real de participantes
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
                'nombre', COALESCE(u.nombre, cp.user_id),
                'rol', cp.rol
            )) AS participantes
        FROM conversacion_participantes cp
        LEFT JOIN usuarios u ON u.id::text = cp.user_id AND u.deleted_at IS NULL
        WHERE cp.conversacion_id IN (SELECT paged.id FROM paged)
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
        WHERE m.conversacion_id IN (SELECT paged.id FROM paged)
          AND m.deleted_at IS NULL
        ORDER BY m.conversacion_id, m.created_at DESC
    ),
    no_leidos AS (
        SELECT
            m.conversacion_id,
            COUNT(*) AS count
        FROM mensajes m
        WHERE m.conversacion_id IN (SELECT paged.id FROM paged)
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
