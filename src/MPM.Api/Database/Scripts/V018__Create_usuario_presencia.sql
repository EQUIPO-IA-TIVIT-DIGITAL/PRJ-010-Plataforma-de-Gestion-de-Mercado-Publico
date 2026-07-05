CREATE TABLE usuario_presencia (
    user_id VARCHAR(100) PRIMARY KEY,
    estado VARCHAR(15) NOT NULL DEFAULT 'offline' CHECK (estado IN ('online', 'offline', 'escribiendo')),
    conversacion_id BIGINT REFERENCES conversaciones(id),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_presencia_estado ON usuario_presencia(estado) WHERE estado != 'offline';
CREATE INDEX idx_presencia_conversacion ON usuario_presencia(conversacion_id) WHERE conversacion_id IS NOT NULL;
