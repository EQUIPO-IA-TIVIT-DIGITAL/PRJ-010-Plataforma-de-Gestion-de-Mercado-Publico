CREATE TABLE conversaciones (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo VARCHAR(10) NOT NULL CHECK (tipo IN ('directo', 'grupal')),
    asunto VARCHAR(200),
    licitacion_id BIGINT REFERENCES licitaciones(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP
);

CREATE INDEX idx_conversaciones_tipo ON conversaciones(tipo) WHERE deleted_at IS NULL;
CREATE INDEX idx_conversaciones_licitacion ON conversaciones(licitacion_id) WHERE deleted_at IS NULL AND licitacion_id IS NOT NULL;
CREATE INDEX idx_conversaciones_updated ON conversaciones(updated_at DESC) WHERE deleted_at IS NULL;
