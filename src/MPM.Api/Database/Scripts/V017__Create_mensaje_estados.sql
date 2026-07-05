CREATE TABLE mensaje_estados (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    mensaje_id BIGINT NOT NULL REFERENCES mensajes(id) ON DELETE CASCADE,
    user_id VARCHAR(100) NOT NULL,
    estado VARCHAR(10) NOT NULL DEFAULT 'entregado' CHECK (estado IN ('entregado', 'leido')),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX uq_mensaje_estados_user_msg ON mensaje_estados(mensaje_id, user_id);
CREATE INDEX idx_mensaje_estados_user ON mensaje_estados(user_id) WHERE estado = 'leido';
