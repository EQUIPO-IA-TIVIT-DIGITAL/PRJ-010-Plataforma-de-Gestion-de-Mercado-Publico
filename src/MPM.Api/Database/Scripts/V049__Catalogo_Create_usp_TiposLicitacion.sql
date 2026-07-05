-- Migración del módulo Catálogo
-- Crea/actualiza la función de consulta de tipos de licitación (idempotente)

CREATE OR REPLACE FUNCTION usp_Catalogos_TiposLicitacion()
RETURNS TABLE(codigo SMALLINT, nombre VARCHAR(50), slug VARCHAR(30))
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY SELECT t.codigo, t.nombre, t.slug FROM tipos_licitacion t ORDER BY t.codigo;
END;
$$;