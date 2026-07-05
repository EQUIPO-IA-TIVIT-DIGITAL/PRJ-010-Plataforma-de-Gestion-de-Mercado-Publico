CREATE TABLE licitaciones (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    codigo_externo VARCHAR(50) NOT NULL,
    nombre VARCHAR(500) NOT NULL,
    descripcion TEXT,
    codigo_estado SMALLINT NOT NULL REFERENCES estados_licitacion(codigo),
    tipo VARCHAR(30) NOT NULL,
    organismo VARCHAR(200),
    unidad_tecnica VARCHAR(200),
    moneda VARCHAR(5) DEFAULT 'CLP',
    monto_estimado DECIMAL(18,2),
    fecha_publicacion TIMESTAMP,
    fecha_cierre TIMESTAMP,
    fecha_adjudicacion TIMESTAMP,
    fecha_estimada_adjudicacion TIMESTAMP,
    link VARCHAR(500),
    raw_data JSONB,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP,

    CONSTRAINT uq_licitaciones_codigo UNIQUE (codigo_externo)
);

CREATE INDEX idx_licitaciones_estado ON licitaciones(codigo_estado) WHERE deleted_at IS NULL;
CREATE INDEX idx_licitaciones_tipo ON licitaciones(tipo) WHERE deleted_at IS NULL;
CREATE INDEX idx_licitaciones_fecha_publicacion ON licitaciones(fecha_publicacion) WHERE deleted_at IS NULL;
CREATE INDEX idx_licitaciones_organismo ON licitaciones(organismo) WHERE deleted_at IS NULL;
CREATE INDEX idx_licitaciones_busqueda ON licitaciones USING gin(
    to_tsvector('spanish', coalesce(nombre,'') || ' ' || coalesce(codigo_externo,''))
);
