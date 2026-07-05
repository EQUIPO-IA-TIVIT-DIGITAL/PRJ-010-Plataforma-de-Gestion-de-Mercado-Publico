-- Migración del módulo Catálogo
-- Crea la tabla de monedas

CREATE TABLE IF NOT EXISTS monedas (
    codigo SMALLINT PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    simbolo VARCHAR(5) NOT NULL,
    codigo_iso VARCHAR(3) NOT NULL UNIQUE
);

CREATE INDEX IF NOT EXISTS idx_monedas_codigo_iso ON monedas(codigo_iso);