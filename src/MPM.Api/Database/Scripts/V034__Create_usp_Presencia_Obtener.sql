CREATE OR REPLACE FUNCTION usp_Presencia_Obtener(
    p_user_ids JSONB
)
RETURNS TABLE (
    UserId VARCHAR(100),
    Estado VARCHAR(15),
    UpdatedAt TIMESTAMP
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_user_id VARCHAR(100);
BEGIN
    FOR v_user_id IN SELECT jsonb_array_elements_text(p_user_ids)
    LOOP
        RETURN QUERY
        SELECT
            up.user_id,
            CASE
                WHEN up.estado = 'escribiendo' AND up.updated_at < (CURRENT_TIMESTAMP - INTERVAL '5 seconds')
                    THEN 'online'::VARCHAR(15)
                WHEN up.estado != 'offline' AND up.updated_at < (CURRENT_TIMESTAMP - INTERVAL '5 minutes')
                    THEN 'offline'::VARCHAR(15)
                ELSE up.estado
            END,
            up.updated_at
        FROM usuario_presencia up
        WHERE up.user_id = v_user_id;

        IF NOT FOUND THEN
            RETURN QUERY SELECT v_user_id, 'offline'::VARCHAR(15), NULL::TIMESTAMP;
        END IF;
    END LOOP;
END;
$$;
