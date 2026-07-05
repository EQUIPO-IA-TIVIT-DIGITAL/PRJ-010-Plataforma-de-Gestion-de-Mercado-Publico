CREATE TABLE mensaje_adjuntos (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    mensaje_id BIGINT NOT NULL REFERENCES mensajes(id) ON DELETE CASCADE,
    nombre_archivo VARCHAR(500) NOT NULL,
    mime_type VARCHAR(200) NOT NULL,
    tamanio_bytes BIGINT NOT NULL,
    ruta_storage VARCHAR(1000) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_adjuntos_mensaje ON mensaje_adjuntos(mensaje_id);
