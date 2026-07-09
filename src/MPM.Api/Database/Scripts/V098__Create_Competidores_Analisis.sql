-- 024-inteligencia-competencia-alertas / US1: cache de analisis de IA por competidor+rango de
-- fechas (research.md R5) -- nunca se dispara Gemini automaticamente, solo cuando el usuario lo
-- pide explicitamente para un competidor+rango; una consulta identica posterior reutiliza esto.

CREATE TABLE IF NOT EXISTS competidores_analisis (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre_competidor VARCHAR(300) NOT NULL,
    fecha_desde DATE NOT NULL,
    fecha_hasta DATE NOT NULL,
    contenido_json JSONB NOT NULL,
    cantidad_licitaciones INT NOT NULL,
    creado_por_usuario_id VARCHAR(100) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_competidores_analisis_nombre_rango UNIQUE (nombre_competidor, fecha_desde, fecha_hasta)
);

CREATE OR REPLACE FUNCTION usp_CompetidoresAnalisis_Buscar(
    p_nombre_competidor VARCHAR(300), p_fecha_desde DATE, p_fecha_hasta DATE
)
RETURNS TABLE(
    p_id BIGINT,
    p_contenido_json JSONB,
    p_cantidad_licitaciones INT,
    p_created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT a.id, a.contenido_json, a.cantidad_licitaciones, a.created_at
    FROM competidores_analisis a
    WHERE a.nombre_competidor = p_nombre_competidor
      AND a.fecha_desde = p_fecha_desde
      AND a.fecha_hasta = p_fecha_hasta;
END;
$$ LANGUAGE plpgsql;

-- ON CONFLICT DO NOTHING: si dos usuarios piden el mismo competidor+rango casi al mismo tiempo,
-- el segundo INSERT no pisa al primero -- el llamador debe volver a leer con usp_..._Buscar
-- despues de guardar para obtener la version que realmente quedo persistida (edge case del spec).
CREATE OR REPLACE FUNCTION usp_CompetidoresAnalisis_Guardar(
    p_nombre_competidor VARCHAR(300),
    p_fecha_desde DATE,
    p_fecha_hasta DATE,
    p_contenido_json JSONB,
    p_cantidad_licitaciones INT,
    p_usuario_id VARCHAR(100)
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO competidores_analisis
        (nombre_competidor, fecha_desde, fecha_hasta, contenido_json, cantidad_licitaciones, creado_por_usuario_id)
    VALUES
        (p_nombre_competidor, p_fecha_desde, p_fecha_hasta, p_contenido_json, p_cantidad_licitaciones, p_usuario_id)
    ON CONFLICT (nombre_competidor, fecha_desde, fecha_hasta) DO NOTHING;
END;
$$ LANGUAGE plpgsql;
