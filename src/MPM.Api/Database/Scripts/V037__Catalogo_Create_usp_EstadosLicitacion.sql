-- Migración del módulo Catálogo
-- Crea/actualiza la función de consulta de estados de licitación (idempotente)
-- Esta función pertenece conceptualmente al módulo Catálogo.
-- Coexiste con la migración original V011 del módulo Licitaciones.

CREATE OR REPLACE FUNCTION usp_Catalogos_EstadosLicitacion()
RETURNS TABLE(codigo SMALLINT, nombre VARCHAR(50))
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY SELECT e.codigo, e.nombre FROM estados_licitacion e ORDER BY e.codigo;
END;
$$;
