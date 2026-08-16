-- V140: la funcion ObtenerCache de actividad de mercado expone updated_at, para que el
-- service pueda distinguir un 'generando' vivo (scraper en curso) de uno estancado
-- (scraper que nunca arranco o murio en el camino: script no publicado, node caido,
-- contenedor reiniciado) y reintentar en vez de dejar al frontend polleando para siempre.

CREATE OR REPLACE FUNCTION usp_CompetidoresActividadMercado_ObtenerCache(
    p_nombre_competidor VARCHAR(300), p_area_codigo SMALLINT, p_fecha_desde DATE, p_fecha_hasta DATE
)
RETURNS TABLE(
    Estado VARCHAR, CantidadLicitaciones INT, MontoTotalAdjudicado NUMERIC,
    ContenidoJson JSONB, GeneradoAt TIMESTAMP, UpdatedAt TIMESTAMP
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT c.estado, c.cantidad_licitaciones, c.monto_total_adjudicado, c.contenido_json, c.generado_at, c.updated_at
    FROM competidores_actividad_mercado c
    WHERE c.nombre_competidor = p_nombre_competidor
      AND c.area_codigo IS NOT DISTINCT FROM p_area_codigo
      AND c.fecha_desde = p_fecha_desde
      AND c.fecha_hasta = p_fecha_hasta;
END;
$$;
