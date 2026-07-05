-- Migración del módulo Catálogo
-- Crea la tabla de tipos de licitación

CREATE TABLE IF NOT EXISTS tipos_licitacion (
    codigo SMALLINT PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    slug VARCHAR(30) NOT NULL UNIQUE,
    descripcion TEXT
);

CREATE INDEX IF NOT EXISTS idx_tipos_licitacion_slug ON tipos_licitacion(slug);