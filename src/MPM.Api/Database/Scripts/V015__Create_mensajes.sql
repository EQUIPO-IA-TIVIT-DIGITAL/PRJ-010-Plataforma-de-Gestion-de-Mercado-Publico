CREATE TABLE mensajes (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    conversacion_id BIGINT NOT NULL REFERENCES conversaciones(id) ON DELETE CASCADE,
    user_id VARCHAR(100) NOT NULL,
    tipo VARCHAR(10) NOT NULL DEFAULT 'texto' CHECK (tipo IN ('texto', 'imagen', 'archivo', 'sistema')),
    contenido TEXT,
    reply_to_id BIGINT REFERENCES mensajes(id),
    edited_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP
);

CREATE INDEX idx_mensajes_conversacion ON mensajes(conversacion_id, created_at DESC) WHERE deleted_at IS NULL;
CREATE INDEX idx_mensajes_user ON mensajes(user_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_mensajes_reply ON mensajes(reply_to_id) WHERE deleted_at IS NULL AND reply_to_id IS NOT NULL;
CREATE INDEX idx_mensajes_busqueda ON mensajes USING gin(
    to_tsvector('spanish', coalesce(contenido, ''))
) WHERE deleted_at IS NULL;
