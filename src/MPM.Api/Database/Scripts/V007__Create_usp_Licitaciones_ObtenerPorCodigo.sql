CREATE OR REPLACE FUNCTION usp_Licitaciones_ObtenerPorCodigo(
    p_codigo_externo VARCHAR(50)
)
RETURNS TABLE(
    licitacion JSONB,
    items JSONB
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_licitacion_id BIGINT;
BEGIN
    SELECT id INTO v_licitacion_id
    FROM licitaciones
    WHERE codigo_externo = p_codigo_externo AND deleted_at IS NULL;

    IF NOT FOUND THEN
        RETURN QUERY SELECT NULL::JSONB, NULL::JSONB;
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        row_to_json(l)::JSONB AS licitacion,
        COALESCE(jsonb_agg(i.*) FILTER (WHERE i.id IS NOT NULL), '[]'::JSONB) AS items
    FROM licitaciones l
    LEFT JOIN licitaciones_items i ON i.licitacion_id = l.id
    WHERE l.id = v_licitacion_id
    GROUP BY l.id;
END;
$$;
