ALTER TABLE catalogo_certificaciones
    ADD COLUMN IF NOT EXISTS tipo VARCHAR(50) NOT NULL DEFAULT 'corporativa',
    ADD COLUMN IF NOT EXISTS titular VARCHAR(200);

CREATE INDEX IF NOT EXISTS idx_catalogo_certificaciones_tipo ON catalogo_certificaciones(tipo);

-- Drop previous function definitions whose return types or parameters change
DROP FUNCTION IF EXISTS usp_CatalogoCertificaciones_Obtener(BIGINT);
DROP FUNCTION IF EXISTS usp_CatalogoCertificaciones_Listar(VARCHAR(250), BOOLEAN, BOOLEAN, INT, INT);
DROP FUNCTION IF EXISTS usp_CatalogoCertificaciones_Listar(VARCHAR(250), BOOLEAN, BOOLEAN, VARCHAR(50), INT, INT);

-- Actualizar SP Obtener Certificación
CREATE OR REPLACE FUNCTION usp_CatalogoCertificaciones_Obtener(p_id BIGINT)
RETURNS TABLE(
    id BIGINT, nombre VARCHAR(250), nombre_normalizado VARCHAR(250), file_id_census VARCHAR(200),
    institucion VARCHAR(200), vigencia VARCHAR(100), titular VARCHAR(200), tipo VARCHAR(50), activo BOOLEAN,
    created_at TIMESTAMP, updated_at TIMESTAMP
)
LANGUAGE SQL
AS $$
    SELECT c.id, c.nombre, c.nombre_normalizado, c.file_id_census, c.institucion, c.vigencia,
           c.titular, c.tipo, c.activo, c.created_at, c.updated_at
    FROM catalogo_certificaciones c WHERE c.id = p_id;
$$;

-- Actualizar SP Listar Certificaciones con soporte para p_tipo y retorno de titular / tipo
CREATE OR REPLACE FUNCTION usp_CatalogoCertificaciones_Listar(
    p_q VARCHAR(250), p_activo BOOLEAN, p_con_archivo BOOLEAN, p_tipo VARCHAR(50), p_offset INT, p_limit INT
)
RETURNS TABLE(
    id BIGINT, nombre VARCHAR(250), nombre_normalizado VARCHAR(250), file_id_census VARCHAR(200),
    institucion VARCHAR(200), vigencia VARCHAR(100), titular VARCHAR(200), tipo VARCHAR(50), activo BOOLEAN,
    created_at TIMESTAMP, updated_at TIMESTAMP, total_count BIGINT
)
LANGUAGE SQL
AS $$
    SELECT c.id, c.nombre, c.nombre_normalizado, c.file_id_census, c.institucion, c.vigencia,
           c.titular, c.tipo, c.activo, c.created_at, c.updated_at, COUNT(*) OVER() AS total_count
    FROM catalogo_certificaciones c
    WHERE (p_q IS NULL OR c.nombre ILIKE '%' || p_q || '%' OR c.institucion ILIKE '%' || p_q || '%')
      AND (p_activo IS NULL OR c.activo = p_activo)
      AND (p_con_archivo IS NULL OR NOT p_con_archivo OR c.file_id_census IS NOT NULL)
      AND (p_tipo IS NULL OR c.tipo = p_tipo)
    ORDER BY c.id ASC
    LIMIT p_limit OFFSET p_offset;
$$;

-- Seed inicial de certificación y experiencia de ejemplo en producción si la tabla está vacía
INSERT INTO catalogo_certificaciones (nombre, nombre_normalizado, institucion, vigencia, titular, tipo, file_id_census, activo, created_at, updated_at)
SELECT '(Ejemplo) ISO/IEC 27001:2022 - Seguridad de la Información (SGSI)',
       'ejemplo iso iec 27001 2022 seguridad de la informacion sgsi',
       '(Ejemplo) Bureau Veritas / Entidad Certificadora',
       '(Ejemplo) 2024 - 2027',
       '(Ejemplo) TIVIT SpA',
       'corporativa',
       NULL,
       true,
       CURRENT_TIMESTAMP,
       CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM catalogo_certificaciones LIMIT 1);

INSERT INTO catalogo_experiencias (titulo, cliente, descripcion, pais, activo, created_at, updated_at)
SELECT '(Ejemplo) Migración Cloud Multi-Región y Administración 24/7',
       '(Ejemplo) Banco / Empresa Corporativa',
       'Implementación y administración de infraestructura en la nube multi-región con alta disponibilidad y monitoreo 24/7.',
       'Chile',
       true,
       CURRENT_TIMESTAMP,
       CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM catalogo_experiencias LIMIT 1);
