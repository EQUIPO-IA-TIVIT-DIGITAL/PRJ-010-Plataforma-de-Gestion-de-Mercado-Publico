-- V062: Crear tablas para adjuntos de licitaciones y control del scraper

CREATE TABLE IF NOT EXISTS licitaciones_adjuntos (
    id BIGSERIAL PRIMARY KEY,
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id),
    tipo VARCHAR(50) NOT NULL,
    nombre_archivo VARCHAR(500) NOT NULL,
    nombre_elemento VARCHAR(500),
    ruta_storage TEXT NOT NULL,
    ruta_local TEXT,
    tamanio_bytes BIGINT,
    mime_type VARCHAR(100),
    grid_origen VARCHAR(30),
    acta_descargada BOOLEAN DEFAULT FALSE,
    analisis_estado VARCHAR(20) DEFAULT 'pendiente',
    analisis_workspace_id BIGINT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    record_status SMALLINT DEFAULT 1
);

CREATE TABLE IF NOT EXISTS scraper_sync_log (
    id BIGSERIAL PRIMARY KEY,
    tipo VARCHAR(20) NOT NULL DEFAULT 'SCRAPER',
    fecha_desde TIMESTAMP,
    fecha_hasta TIMESTAMP,
    registros_procesados INT DEFAULT 0,
    nuevos INT DEFAULT 0,
    actualizados INT DEFAULT 0,
    errores INT DEFAULT 0,
    detalle_errores JSONB,
    total_licitaciones INT DEFAULT 0,
    total_con_acta INT DEFAULT 0,
    total_sin_acta INT DEFAULT 0,
    total_analizados INT DEFAULT 0,
    ejecutado_en TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    duracion_ms BIGINT,
    estado VARCHAR(10) NOT NULL DEFAULT 'iniciado'
);

CREATE INDEX IF NOT EXISTS idx_adjuntos_licitacion ON licitaciones_adjuntos(licitacion_id);
CREATE INDEX IF NOT EXISTS idx_adjuntos_tipo ON licitaciones_adjuntos(tipo);
CREATE INDEX IF NOT EXISTS idx_adjuntos_estado ON licitaciones_adjuntos(analisis_estado);
CREATE INDEX IF NOT EXISTS idx_scraper_sync_tipo ON scraper_sync_log(tipo);
CREATE INDEX IF NOT EXISTS idx_scraper_sync_fecha ON scraper_sync_log(ejecutado_en);