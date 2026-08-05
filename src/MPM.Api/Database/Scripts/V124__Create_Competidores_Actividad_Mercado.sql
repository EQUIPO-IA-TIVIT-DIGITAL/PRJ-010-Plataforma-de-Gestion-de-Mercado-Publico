-- V124: US4 (spec 031) — cache de "actividad total de mercado" de un competidor por
-- área+período. Mismo patrón de cache que competidores_analisis (V098), pero el contenido
-- se genera con un scrape acotado (competidor-mercado.js), no con Gemini -- ver
-- research.md §4 y contracts/competidores-actividad-mercado.md.

CREATE TABLE IF NOT EXISTS competidores_actividad_mercado (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre_competidor VARCHAR(300) NOT NULL,
    area_codigo SMALLINT NULL REFERENCES areas_negocio(codigo),
    fecha_desde DATE NOT NULL,
    fecha_hasta DATE NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'generando' CHECK (estado IN ('generando', 'listo', 'error')),
    cantidad_licitaciones INT NULL,
    monto_total_adjudicado NUMERIC(18,2) NULL,
    contenido_json JSONB NULL,
    generado_at TIMESTAMP NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_competidores_actividad_mercado UNIQUE (nombre_competidor, area_codigo, fecha_desde, fecha_hasta)
);

CREATE OR REPLACE FUNCTION usp_CompetidoresActividadMercado_ObtenerCache(
    p_nombre_competidor VARCHAR(300), p_area_codigo SMALLINT, p_fecha_desde DATE, p_fecha_hasta DATE
)
RETURNS TABLE(
    Estado VARCHAR, CantidadLicitaciones INT, MontoTotalAdjudicado NUMERIC,
    ContenidoJson JSONB, GeneradoAt TIMESTAMP
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT c.estado, c.cantidad_licitaciones, c.monto_total_adjudicado, c.contenido_json, c.generado_at
    FROM competidores_actividad_mercado c
    WHERE c.nombre_competidor = p_nombre_competidor
      AND c.area_codigo IS NOT DISTINCT FROM p_area_codigo
      AND c.fecha_desde = p_fecha_desde
      AND c.fecha_hasta = p_fecha_hasta;
END;
$$;

-- Encola: crea la fila en 'generando' si no existe (idempotente -- si dos requests casi
-- simultáneos piden lo mismo, el segundo no vuelve a encolar, ver contracts/).
CREATE OR REPLACE FUNCTION usp_CompetidoresActividadMercado_Encolar(
    p_nombre_competidor VARCHAR(300), p_area_codigo SMALLINT, p_fecha_desde DATE, p_fecha_hasta DATE
)
RETURNS VOID
LANGUAGE sql
AS $$
    INSERT INTO competidores_actividad_mercado (nombre_competidor, area_codigo, fecha_desde, fecha_hasta)
    VALUES (p_nombre_competidor, p_area_codigo, p_fecha_desde, p_fecha_hasta)
    ON CONFLICT (nombre_competidor, area_codigo, fecha_desde, fecha_hasta) DO NOTHING;
$$;

CREATE OR REPLACE FUNCTION usp_CompetidoresActividadMercado_Guardar(
    p_nombre_competidor VARCHAR(300), p_area_codigo SMALLINT, p_fecha_desde DATE, p_fecha_hasta DATE,
    p_cantidad_licitaciones INT, p_monto_total_adjudicado NUMERIC, p_contenido_json JSONB
)
RETURNS VOID
LANGUAGE sql
AS $$
    UPDATE competidores_actividad_mercado
    SET estado = 'listo',
        cantidad_licitaciones = p_cantidad_licitaciones,
        monto_total_adjudicado = p_monto_total_adjudicado,
        contenido_json = p_contenido_json,
        generado_at = CURRENT_TIMESTAMP,
        updated_at = CURRENT_TIMESTAMP
    WHERE nombre_competidor = p_nombre_competidor
      AND area_codigo IS NOT DISTINCT FROM p_area_codigo
      AND fecha_desde = p_fecha_desde
      AND fecha_hasta = p_fecha_hasta;
$$;

CREATE OR REPLACE FUNCTION usp_CompetidoresActividadMercado_MarcarError(
    p_nombre_competidor VARCHAR(300), p_area_codigo SMALLINT, p_fecha_desde DATE, p_fecha_hasta DATE
)
RETURNS VOID
LANGUAGE sql
AS $$
    UPDATE competidores_actividad_mercado
    SET estado = 'error', updated_at = CURRENT_TIMESTAMP
    WHERE nombre_competidor = p_nombre_competidor
      AND area_codigo IS NOT DISTINCT FROM p_area_codigo
      AND fecha_desde = p_fecha_desde
      AND fecha_hasta = p_fecha_hasta;
$$;
