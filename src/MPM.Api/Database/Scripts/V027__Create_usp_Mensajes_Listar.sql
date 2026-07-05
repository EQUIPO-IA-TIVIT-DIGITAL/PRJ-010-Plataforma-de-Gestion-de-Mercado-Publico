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
        WHERE ma.mensaje_id IN (SELECT id FROM paged)
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
        WHERE me.mensaje_id IN (SELECT id FROM paged)
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
