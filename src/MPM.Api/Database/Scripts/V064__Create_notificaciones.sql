-- V064: Crear tabla de notificaciones del sistema

CREATE TABLE IF NOT EXISTS notificaciones (
    id BIGSERIAL PRIMARY KEY,
    usuario_id TEXT NOT NULL,
    tipo VARCHAR(50) NOT NULL,
    titulo TEXT NOT NULL,
    mensaje TEXT NOT NULL,
    metadata JSONB,
    leido BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status SMALLINT NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS idx_notificaciones_usuario ON notificaciones(usuario_id);
CREATE INDEX IF NOT EXISTS idx_notificaciones_usuario_leido ON notificaciones(usuario_id, leido);
CREATE INDEX IF NOT EXISTS idx_notificaciones_created ON notificaciones(created_at DESC);
