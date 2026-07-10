-- 024-inteligencia-competencia-alertas / US1: listado de nombres de competidores distintos ya
-- recolectados en licitaciones_ofertas, para poblar un dropdown de búsqueda en vez de texto
-- libre. Excluye a TIVIT (no tiene sentido comparar a TIVIT contra sí mismo).

CREATE OR REPLACE FUNCTION usp_LicitacionesOfertas_ListarCompetidores()
RETURNS TABLE(p_nombre_proveedor VARCHAR(300)) AS $$
BEGIN
    RETURN QUERY
    SELECT DISTINCT o.nombre_proveedor
    FROM licitaciones_ofertas o
    WHERE o.nombre_proveedor NOT ILIKE '%tivit%'
    ORDER BY o.nombre_proveedor;
END;
$$ LANGUAGE plpgsql;
