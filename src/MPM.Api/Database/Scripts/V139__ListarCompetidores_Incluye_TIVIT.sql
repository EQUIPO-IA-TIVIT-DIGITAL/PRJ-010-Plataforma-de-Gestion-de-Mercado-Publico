-- V139: incluye a TIVIT en el dropdown de competidores de /competidores.
-- Antes (V100) se excluia a TIVIT ("no tiene sentido comparar contra si mismo"); ahora el
-- negocio quiere poder seleccionar a TIVIT para ver su propia actividad de mercado
-- (actividad-mercado incluye licitaciones donde TIVIT participo y brechas donde no).
-- usp_LicitacionesOfertas_BuscarPorCompetidor (V097) no excluye a TIVIT, asi que la
-- busqueda de ofertas funciona igual una vez que aparece en el listado.

CREATE OR REPLACE FUNCTION usp_LicitacionesOfertas_ListarCompetidores()
RETURNS TABLE(p_nombre_proveedor VARCHAR(300)) AS $$
BEGIN
    RETURN QUERY
    SELECT DISTINCT o.nombre_proveedor
    FROM licitaciones_ofertas o
    ORDER BY o.nombre_proveedor;
END;
$$ LANGUAGE plpgsql;
