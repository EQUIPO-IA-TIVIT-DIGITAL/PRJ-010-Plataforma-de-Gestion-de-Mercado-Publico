-- 024-inteligencia-competencia-alertas / US1: listado de oferentes (no solo el adjudicatario)
-- por licitacion, recolectado del "Cuadro de Ofertas" publico de Mercado Publico (confirmado
-- en vivo 2026-07-09 que no requiere login). Permite buscar "en que ha ofertado un competidor".

CREATE TABLE IF NOT EXISTS licitaciones_ofertas (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id),
    rut_proveedor VARCHAR(20),
    nombre_proveedor VARCHAR(300) NOT NULL,
    monto_oferta NUMERIC(18,2),
    estado_oferta VARCHAR(30),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_licitaciones_ofertas_licitacion_proveedor UNIQUE (licitacion_id, rut_proveedor)
);

CREATE INDEX IF NOT EXISTS idx_licitaciones_ofertas_licitacion ON licitaciones_ofertas(licitacion_id);

-- pg_trgm ya se instalo en V093 (fix de busqueda de licitaciones) -- se reusa para buscar
-- competidor por nombre con coincidencia flexible (research.md R4).
CREATE INDEX IF NOT EXISTS idx_licitaciones_ofertas_proveedor_trgm
    ON licitaciones_ofertas USING gin (nombre_proveedor gin_trgm_ops);

CREATE OR REPLACE FUNCTION usp_LicitacionesOfertas_Guardar(
    p_licitacion_id BIGINT,
    p_rut_proveedor VARCHAR(20),
    p_nombre_proveedor VARCHAR(300),
    p_monto_oferta NUMERIC(18,2),
    p_estado_oferta VARCHAR(30)
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO licitaciones_ofertas (licitacion_id, rut_proveedor, nombre_proveedor, monto_oferta, estado_oferta)
    VALUES (p_licitacion_id, p_rut_proveedor, p_nombre_proveedor, p_monto_oferta, p_estado_oferta)
    ON CONFLICT (licitacion_id, rut_proveedor) DO UPDATE
        SET nombre_proveedor = p_nombre_proveedor,
            monto_oferta = p_monto_oferta,
            estado_oferta = p_estado_oferta,
            updated_at = CURRENT_TIMESTAMP;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_LicitacionesOfertas_BuscarPorCompetidor(p_nombre VARCHAR(300))
RETURNS TABLE(
    p_licitacion_id BIGINT,
    p_codigo_externo VARCHAR(50),
    p_nombre_licitacion VARCHAR(500),
    p_organismo VARCHAR(200),
    p_fecha_cierre TIMESTAMP,
    p_rut_proveedor VARCHAR(20),
    p_nombre_proveedor VARCHAR(300),
    p_monto_oferta NUMERIC(18,2),
    p_estado_oferta VARCHAR(30)
) AS $$
BEGIN
    RETURN QUERY
    SELECT l.id, l.codigo_externo, l.nombre, l.organismo, l.fecha_cierre,
           o.rut_proveedor, o.nombre_proveedor, o.monto_oferta, o.estado_oferta
    FROM licitaciones_ofertas o
    JOIN licitaciones l ON l.id = o.licitacion_id
    WHERE o.nombre_proveedor ILIKE '%' || p_nombre || '%'
      AND l.deleted_at IS NULL
    ORDER BY l.fecha_cierre DESC NULLS LAST;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_LicitacionesOfertas_ContarPorCompetidorYRango(
    p_nombre VARCHAR(300), p_fecha_desde DATE, p_fecha_hasta DATE
)
RETURNS INT AS $$
DECLARE
    v_count INT;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM licitaciones_ofertas o
    JOIN licitaciones l ON l.id = o.licitacion_id
    WHERE o.nombre_proveedor ILIKE '%' || p_nombre || '%'
      AND l.deleted_at IS NULL
      AND l.fecha_cierre::date BETWEEN p_fecha_desde AND p_fecha_hasta;
    RETURN v_count;
END;
$$ LANGUAGE plpgsql;
