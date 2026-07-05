-- Migración del módulo Catálogo
-- Crea la tabla de estados de licitación si no existe (idempotente)
-- Esta migración permite que el módulo Catálogo sea autónomo,
-- coexistiendo con la migración original V001 del módulo Licitaciones.

CREATE TABLE IF NOT EXISTS estados_licitacion (
    codigo SMALLINT PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    descripcion TEXT
);
