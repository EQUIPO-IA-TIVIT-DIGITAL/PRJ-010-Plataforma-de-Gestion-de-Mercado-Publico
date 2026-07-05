CREATE TABLE conversacion_participantes (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    conversacion_id BIGINT NOT NULL REFERENCES conversaciones(id) ON DELETE CASCADE,
    user_id VARCHAR(100) NOT NULL,
    rol VARCHAR(10) NOT NULL DEFAULT 'miembro' CHECK (rol IN ('admin', 'miembro')),
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    left_at TIMESTAMP
);

CREATE INDEX idx_conv_participantes_conversacion ON conversacion_participantes(conversacion_id) WHERE left_at IS NULL;
CREATE INDEX idx_conv_participantes_user ON conversacion_participantes(user_id) WHERE left_at IS NULL;
CREATE UNIQUE INDEX uq_conv_participantes_user_conv ON conversacion_participantes(conversacion_id, user_id) WHERE left_at IS NULL;
