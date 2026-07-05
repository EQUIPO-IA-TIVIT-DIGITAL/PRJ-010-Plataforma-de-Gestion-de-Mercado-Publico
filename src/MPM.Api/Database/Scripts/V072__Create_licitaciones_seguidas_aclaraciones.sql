-- V072: Seguimiento de licitaciones activas y registro de aclaraciones detectadas

CREATE TABLE IF NOT EXISTS licitaciones_seguidas (
    id             BIGSERIAL PRIMARY KEY,
    usuario_id     TEXT NOT NULL,
    codigo_externo VARCHAR(50) NOT NULL,
    created_at     TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(usuario_id, codigo_externo)
);

CREATE INDEX IF NOT EXISTS idx_licitaciones_seguidas_usuario ON licitaciones_seguidas(usuario_id);
CREATE INDEX IF NOT EXISTS idx_licitaciones_seguidas_codigo ON licitaciones_seguidas(codigo_externo);

CREATE TABLE IF NOT EXISTS licitaciones_aclaraciones (
    id                BIGSERIAL PRIMARY KEY,
    codigo_externo    VARCHAR(50) NOT NULL,
    codigo_aclaracion INT NOT NULL,
    pregunta          TEXT,
    respuesta         TEXT,
    fecha_publicacion TIMESTAMP,
    fecha_respuesta   TIMESTAMP,
    notificado        BOOLEAN NOT NULL DEFAULT FALSE,
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(codigo_externo, codigo_aclaracion)
);

CREATE INDEX IF NOT EXISTS idx_licitaciones_aclaraciones_codigo ON licitaciones_aclaraciones(codigo_externo);
CREATE INDEX IF NOT EXISTS idx_licitaciones_aclaraciones_no_notificadas ON licitaciones_aclaraciones(notificado) WHERE notificado = FALSE;
