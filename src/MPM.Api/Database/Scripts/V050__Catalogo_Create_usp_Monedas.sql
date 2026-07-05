-- Migración del módulo Catálogo
-- Crea/actualiza la función de consulta de monedas (idempotente)

CREATE OR REPLACE FUNCTION usp_Catalogos_Monedas()
RETURNS TABLE(codigo SMALLINT, nombre VARCHAR(50), simbolo VARCHAR(5), codigo_iso VARCHAR(3))
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY SELECT m.codigo, m.nombre, m.simbolo, m.codigo_iso FROM monedas m ORDER BY m.codigo;
END;
$$;