CREATE OR REPLACE FUNCTION usp_Licitaciones_Buscar(
    p_q VARCHAR(100),
    p_limit INT DEFAULT 10
)
RETURNS TABLE(
    codigo_externo VARCHAR(50),
    nombre VARCHAR(500),
    tipo VARCHAR(30),
    codigo_estado SMALLINT,
    organismo VARCHAR(200)
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF LENGTH(TRIM(p_q)) < 3 THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT l.codigo_externo, l.nombre, l.tipo, l.codigo_estado, l.organismo
    FROM licitaciones l
    WHERE l.deleted_at IS NULL
      AND (
          l.codigo_externo ILIKE '%' || p_q || '%'
          OR l.nombre ILIKE '%' || p_q || '%'
      )
    ORDER BY
        CASE WHEN l.codigo_externo ILIKE p_q || '%' THEN 0 ELSE 1 END,
        l.nombre
    LIMIT LEAST(GREATEST(p_limit, 1), 50);
END;
$$;
