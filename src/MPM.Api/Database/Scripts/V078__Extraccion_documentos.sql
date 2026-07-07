-- V078: Extracción de documentos vía HTTP directo (spec 016-extraccion-documentos-api)
-- Agrega trazabilidad de qué método obtuvo cada adjunto (directo vs. navegador) y
-- una tabla de log para comparar cobertura entre ambos métodos (US3 del spec).

ALTER TABLE licitaciones_adjuntos
    ADD COLUMN IF NOT EXISTS metodo_extraccion VARCHAR(20) NOT NULL DEFAULT 'navegador';

CREATE TABLE IF NOT EXISTS extraccion_documentos_log (
    id BIGSERIAL PRIMARY KEY,
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id),
    metodo VARCHAR(20) NOT NULL,          -- 'directo' | 'navegador'
    estado VARCHAR(20) NOT NULL,          -- 'exito' | 'fallo' | 'sin_adjuntos'
    documentos_obtenidos INT DEFAULT 0,
    acta_obtenida BOOLEAN DEFAULT FALSE,
    es_fallback BOOLEAN DEFAULT FALSE,
    error TEXT,
    duracion_ms BIGINT,
    ejecutado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_extraccion_log_licitacion ON extraccion_documentos_log(licitacion_id);
CREATE INDEX IF NOT EXISTS idx_extraccion_log_metodo_estado ON extraccion_documentos_log(metodo, estado);
CREATE INDEX IF NOT EXISTS idx_extraccion_log_fecha ON extraccion_documentos_log(ejecutado_en);

CREATE OR REPLACE FUNCTION usp_ExtraccionLog_Registrar(
    p_licitacion_id BIGINT,
    p_metodo VARCHAR(20),
    p_estado VARCHAR(20),
    p_documentos_obtenidos INT,
    p_acta_obtenida BOOLEAN,
    p_es_fallback BOOLEAN,
    p_error TEXT,
    p_duracion_ms BIGINT
)
RETURNS TABLE(p_id BIGINT) AS $$
BEGIN
    RETURN QUERY
    INSERT INTO extraccion_documentos_log
        (licitacion_id, metodo, estado, documentos_obtenidos, acta_obtenida, es_fallback, error, duracion_ms)
    VALUES
        (p_licitacion_id, p_metodo, p_estado, p_documentos_obtenidos, p_acta_obtenida, p_es_fallback, p_error, p_duracion_ms)
    RETURNING id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_ExtraccionLog_ResumenPeriodo(
    p_desde TIMESTAMP,
    p_hasta TIMESTAMP
)
RETURNS TABLE(
    p_metodo VARCHAR(20),
    p_estado VARCHAR(20),
    p_total BIGINT,
    p_promedio_duracion_ms NUMERIC
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        l.metodo,
        l.estado,
        COUNT(*)::BIGINT,
        AVG(l.duracion_ms)
    FROM extraccion_documentos_log l
    WHERE l.ejecutado_en BETWEEN p_desde AND p_hasta
    GROUP BY l.metodo, l.estado;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Adjuntos_RegistrarDirecto(
    p_licitacion_id BIGINT,
    p_tipo VARCHAR(50),
    p_nombre_archivo VARCHAR(500),
    p_ruta_storage TEXT,
    p_tamanio_bytes BIGINT,
    p_mime_type VARCHAR(100),
    p_es_acta BOOLEAN
)
RETURNS TABLE(p_id BIGINT) AS $$
BEGIN
    RETURN QUERY
    INSERT INTO licitaciones_adjuntos
        (licitacion_id, tipo, nombre_archivo, ruta_storage, tamanio_bytes, mime_type,
         acta_descargada, metodo_extraccion, grid_origen)
    VALUES
        (p_licitacion_id, p_tipo, p_nombre_archivo, p_ruta_storage, p_tamanio_bytes, p_mime_type,
         p_es_acta, 'directo', 'DWNL_grdId')
    RETURNING id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION usp_Adjuntos_ExistePorLicitacion(
    p_licitacion_id BIGINT
)
RETURNS TABLE(p_existe BOOLEAN) AS $$
BEGIN
    RETURN QUERY
    SELECT EXISTS(
        SELECT 1 FROM licitaciones_adjuntos
        WHERE licitacion_id = p_licitacion_id AND record_status = 1
    );
END;
$$ LANGUAGE plpgsql;
