CREATE OR REPLACE FUNCTION usp_Conversaciones_Obtener(
    p_id BIGINT,
    p_user_id VARCHAR(100)
)
RETURNS TABLE (
    Id BIGINT,
    Tipo VARCHAR(10),
    Asunto VARCHAR(200),
    LicitacionId BIGINT,
    LicitacionNombre VARCHAR(500),
    Participantes JSONB,
    CreatedAt TIMESTAMP,
    UpdatedAt TIMESTAMP
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM conversaciones c
        WHERE c.id = p_id AND c.deleted_at IS NULL
    ) THEN
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM conversacion_participantes cp
        WHERE cp.conversacion_id = p_id
          AND cp.user_id = p_user_id
          AND cp.left_at IS NULL
    ) THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        c.id,
        c.tipo,
        c.asunto,
        c.licitacion_id,
        l.nombre,
        (
            SELECT jsonb_agg(jsonb_build_object(
                'userId', cp.user_id,
                'nombre', cp.user_id,
                'rol', cp.rol,
                'joinedAt', cp.joined_at
            ))
            FROM conversacion_participantes cp
            WHERE cp.conversacion_id = c.id AND cp.left_at IS NULL
        ),
        c.created_at,
        c.updated_at
    FROM conversaciones c
    LEFT JOIN licitaciones l ON l.id = c.licitacion_id
    WHERE c.id = p_id;
END;
$$;
