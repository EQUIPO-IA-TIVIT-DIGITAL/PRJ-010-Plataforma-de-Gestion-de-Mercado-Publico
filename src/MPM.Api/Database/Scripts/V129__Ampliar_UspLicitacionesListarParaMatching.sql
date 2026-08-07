-- 032-mejora-alertas-correo (US2): el correo de alerta hoy solo trae keyword+nombre+codigo+
-- presupuesto opcional. fecha_cierre y link ya existen como columnas reales en licitaciones
-- (V002__Create_licitaciones.sql) -- solo hace falta traerlas en el SELECT que ya usa el
-- matching de alertas, sin agregar ninguna consulta nueva.
DROP FUNCTION IF EXISTS usp_Licitaciones_ListarParaMatching(TIMESTAMPTZ);

CREATE OR REPLACE FUNCTION usp_Licitaciones_ListarParaMatching(
    p_fecha_desde TIMESTAMPTZ
)
RETURNS TABLE(
    p_id BIGINT, p_codigo_externo VARCHAR(50), p_nombre VARCHAR(500), p_descripcion TEXT,
    p_monto_estimado DECIMAL(18,2), p_tipo VARCHAR(30), p_organismo VARCHAR(200),
    p_fecha_cierre TIMESTAMP, p_link VARCHAR(500)
) AS $$
BEGIN
    RETURN QUERY
    SELECT l.id, l.codigo_externo, l.nombre, l.descripcion, l.monto_estimado, l.tipo, l.organismo,
           l.fecha_cierre, l.link
    FROM licitaciones l
    WHERE l.fecha_publicacion >= p_fecha_desde::TIMESTAMP AND l.deleted_at IS NULL;
END;
$$ LANGUAGE plpgsql;
