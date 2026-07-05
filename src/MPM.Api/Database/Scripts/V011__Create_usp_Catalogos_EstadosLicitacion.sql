CREATE OR REPLACE FUNCTION usp_Catalogos_EstadosLicitacion()
RETURNS TABLE(codigo SMALLINT, nombre VARCHAR(50))
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY SELECT e.codigo, e.nombre FROM estados_licitacion e ORDER BY e.codigo;
END;
$$;
