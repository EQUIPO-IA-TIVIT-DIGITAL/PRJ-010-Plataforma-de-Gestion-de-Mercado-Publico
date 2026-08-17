-- V145: catálogo corporativo de propuestas y sincronización de certificaciones Census.
-- Bundle A (036-flujo-comercial-ofertas). Este script es re-ejecutable: las tablas,
-- funciones, índices y semillas usan IF NOT EXISTS/CREATE OR REPLACE/UPSERT.

ALTER TABLE licitaciones_interes
    ADD COLUMN IF NOT EXISTS notificado_at TIMESTAMP;

CREATE TABLE IF NOT EXISTS catalogo_experiencias (
    id BIGSERIAL PRIMARY KEY,
    titulo VARCHAR(250) NOT NULL,
    cliente VARCHAR(250) NOT NULL,
    descripcion TEXT,
    fecha_inicio DATE,
    fecha_fin DATE,
    monto_usd NUMERIC(16,2),
    pais VARCHAR(100),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS catalogo_certificaciones (
    id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(250) NOT NULL,
    -- Se conserva el nombre visible y se usa esta clave estable para equivalencias
    -- como ISO/IEC 27001, ISO 27001 y 27001.
    nombre_normalizado VARCHAR(250) NOT NULL,
    file_id_census VARCHAR(200),
    institucion VARCHAR(200),
    vigencia VARCHAR(100),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_catalogo_certificaciones_nombre_normalizado UNIQUE (nombre_normalizado)
);

CREATE TABLE IF NOT EXISTS catalogo_capitulos (
    id BIGSERIAL PRIMARY KEY,
    titulo VARCHAR(250) NOT NULL,
    contenido_markdown TEXT,
    orden INT NOT NULL DEFAULT 0,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_catalogo_capitulos_orden UNIQUE (orden)
);

CREATE TABLE IF NOT EXISTS propuestas (
    id BIGSERIAL PRIMARY KEY,
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id),
    version INT NOT NULL DEFAULT 1,
    capitulos_seleccionados JSONB NOT NULL DEFAULT '[]'::JSONB,
    certificaciones_ids JSONB NOT NULL DEFAULT '[]'::JSONB,
    experiencias_ids JSONB NOT NULL DEFAULT '[]'::JSONB,
    ruta_archivo VARCHAR(500),
    estado VARCHAR(20) NOT NULL DEFAULT 'borrador',
    generado_por VARCHAR(200),
    generado_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_propuestas_licitacion_version UNIQUE (licitacion_id, version),
    CONSTRAINT ck_propuestas_estado CHECK (estado IN ('borrador', 'generada', 'enviada', 'descartada')),
    CONSTRAINT ck_propuestas_version_positiva CHECK (version > 0)
);

CREATE INDEX IF NOT EXISTS idx_catalogo_experiencias_activo
    ON catalogo_experiencias(activo);
CREATE INDEX IF NOT EXISTS idx_catalogo_certificaciones_activo
    ON catalogo_certificaciones(activo);
CREATE INDEX IF NOT EXISTS idx_catalogo_certificaciones_archivo
    ON catalogo_certificaciones(file_id_census) WHERE file_id_census IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_catalogo_capitulos_orden
    ON catalogo_capitulos(orden);
CREATE INDEX IF NOT EXISTS idx_catalogo_capitulos_activo
    ON catalogo_capitulos(activo);
CREATE INDEX IF NOT EXISTS idx_propuestas_licitacion
    ON propuestas(licitacion_id);
CREATE INDEX IF NOT EXISTS idx_propuestas_estado
    ON propuestas(estado);

-- ── Catálogo de experiencias ─────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION usp_CatalogoExperiencias_Obtener(p_id BIGINT)
RETURNS TABLE(
    id BIGINT, titulo VARCHAR(250), cliente VARCHAR(250), descripcion TEXT,
    fecha_inicio DATE, fecha_fin DATE, monto_usd NUMERIC(16,2), pais VARCHAR(100),
    activo BOOLEAN, created_at TIMESTAMP, updated_at TIMESTAMP
)
LANGUAGE SQL
AS $$
    SELECT e.id, e.titulo, e.cliente, e.descripcion, e.fecha_inicio, e.fecha_fin,
           e.monto_usd, e.pais, e.activo, e.created_at, e.updated_at
    FROM catalogo_experiencias e WHERE e.id = p_id;
$$;

CREATE OR REPLACE FUNCTION usp_CatalogoExperiencias_Listar(
    p_q VARCHAR(250), p_activo BOOLEAN, p_offset INT, p_limit INT
)
RETURNS TABLE(
    id BIGINT, titulo VARCHAR(250), cliente VARCHAR(250), descripcion TEXT,
    fecha_inicio DATE, fecha_fin DATE, monto_usd NUMERIC(16,2), pais VARCHAR(100),
    activo BOOLEAN, created_at TIMESTAMP, updated_at TIMESTAMP, total_count BIGINT
)
LANGUAGE SQL
AS $$
    SELECT e.id, e.titulo, e.cliente, e.descripcion, e.fecha_inicio, e.fecha_fin,
           e.monto_usd, e.pais, e.activo, e.created_at, e.updated_at,
           COUNT(*) OVER() AS total_count
    FROM catalogo_experiencias e
    WHERE (p_q IS NULL OR e.titulo ILIKE '%' || p_q || '%' OR e.cliente ILIKE '%' || p_q || '%')
      AND (p_activo IS NULL OR e.activo = p_activo)
    ORDER BY e.titulo, e.id
    LIMIT p_limit OFFSET p_offset;
$$;

CREATE OR REPLACE PROCEDURE usp_CatalogoExperiencias_Insertar(
    p_titulo VARCHAR(250), p_cliente VARCHAR(250), p_descripcion TEXT,
    p_fecha_inicio DATE, p_fecha_fin DATE, p_monto_usd NUMERIC(16,2), p_pais VARCHAR(100),
    INOUT p_id BIGINT DEFAULT 0, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO catalogo_experiencias
        (titulo, cliente, descripcion, fecha_inicio, fecha_fin, monto_usd, pais)
    VALUES (p_titulo, p_cliente, p_descripcion, p_fecha_inicio, p_fecha_fin, p_monto_usd, p_pais)
    RETURNING id INTO p_id;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_CatalogoExperiencias_Actualizar(
    p_id BIGINT, p_titulo VARCHAR(250), p_cliente VARCHAR(250), p_descripcion TEXT,
    p_fecha_inicio DATE, p_fecha_fin DATE, p_monto_usd NUMERIC(16,2), p_pais VARCHAR(100),
    p_activo BOOLEAN, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE catalogo_experiencias
    SET titulo = p_titulo, cliente = p_cliente, descripcion = p_descripcion,
        fecha_inicio = p_fecha_inicio, fecha_fin = p_fecha_fin, monto_usd = p_monto_usd,
        pais = p_pais, activo = p_activo, updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id;
    IF NOT FOUND THEN p_error_msg := 'PRO_001:Experiencia no encontrada'; RETURN; END IF;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_CatalogoExperiencias_Eliminar(
    p_id BIGINT, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE catalogo_experiencias SET activo = FALSE, updated_at = CURRENT_TIMESTAMP WHERE id = p_id;
    IF NOT FOUND THEN p_error_msg := 'PRO_001:Experiencia no encontrada'; RETURN; END IF;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- ── Catálogo de certificaciones ──────────────────────────────────────────────
CREATE OR REPLACE FUNCTION usp_CatalogoCertificaciones_Obtener(p_id BIGINT)
RETURNS TABLE(
    id BIGINT, nombre VARCHAR(250), nombre_normalizado VARCHAR(250), file_id_census VARCHAR(200),
    institucion VARCHAR(200), vigencia VARCHAR(100), activo BOOLEAN,
    created_at TIMESTAMP, updated_at TIMESTAMP
)
LANGUAGE SQL
AS $$
    SELECT c.id, c.nombre, c.nombre_normalizado, c.file_id_census, c.institucion, c.vigencia,
           c.activo, c.created_at, c.updated_at
    FROM catalogo_certificaciones c WHERE c.id = p_id;
$$;

CREATE OR REPLACE FUNCTION usp_CatalogoCertificaciones_Listar(
    p_q VARCHAR(250), p_activo BOOLEAN, p_con_archivo BOOLEAN, p_offset INT, p_limit INT
)
RETURNS TABLE(
    id BIGINT, nombre VARCHAR(250), nombre_normalizado VARCHAR(250), file_id_census VARCHAR(200),
    institucion VARCHAR(200), vigencia VARCHAR(100), activo BOOLEAN,
    created_at TIMESTAMP, updated_at TIMESTAMP, total_count BIGINT
)
LANGUAGE SQL
AS $$
    SELECT c.id, c.nombre, c.nombre_normalizado, c.file_id_census, c.institucion, c.vigencia,
           c.activo, c.created_at, c.updated_at, COUNT(*) OVER() AS total_count
    FROM catalogo_certificaciones c
    WHERE (p_q IS NULL OR c.nombre ILIKE '%' || p_q || '%')
      AND (p_activo IS NULL OR c.activo = p_activo)
      AND (p_con_archivo IS NULL OR NOT p_con_archivo OR c.file_id_census IS NOT NULL)
    ORDER BY c.nombre, c.id
    LIMIT p_limit OFFSET p_offset;
$$;

CREATE OR REPLACE PROCEDURE usp_CatalogoCertificaciones_Insertar(
    p_nombre VARCHAR(250), p_nombre_normalizado VARCHAR(250), p_file_id_census VARCHAR(200),
    p_institucion VARCHAR(200), p_vigencia VARCHAR(100),
    INOUT p_id BIGINT DEFAULT 0, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM catalogo_certificaciones WHERE nombre_normalizado = p_nombre_normalizado) THEN
        p_error_msg := 'PRO_002:La certificación ya existe'; RETURN;
    END IF;
    INSERT INTO catalogo_certificaciones
        (nombre, nombre_normalizado, file_id_census, institucion, vigencia)
    VALUES (p_nombre, p_nombre_normalizado, p_file_id_census, p_institucion, p_vigencia)
    RETURNING id INTO p_id;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_CatalogoCertificaciones_Actualizar(
    p_id BIGINT, p_nombre VARCHAR(250), p_nombre_normalizado VARCHAR(250),
    p_file_id_census VARCHAR(200), p_institucion VARCHAR(200), p_vigencia VARCHAR(100),
    p_activo BOOLEAN, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM catalogo_certificaciones
               WHERE nombre_normalizado = p_nombre_normalizado AND id <> p_id) THEN
        p_error_msg := 'PRO_002:La certificación ya existe'; RETURN;
    END IF;
    UPDATE catalogo_certificaciones
    SET nombre = p_nombre, nombre_normalizado = p_nombre_normalizado,
        file_id_census = p_file_id_census, institucion = p_institucion, vigencia = p_vigencia,
        activo = p_activo, updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id;
    IF NOT FOUND THEN p_error_msg := 'PRO_001:Certificación no encontrada'; RETURN; END IF;
    p_error_msg := NULL;
EXCEPTION WHEN unique_violation THEN
    p_error_msg := 'PRO_002:La certificación ya existe';
WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_CatalogoCertificaciones_Eliminar(
    p_id BIGINT, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE catalogo_certificaciones SET activo = FALSE, updated_at = CURRENT_TIMESTAMP WHERE id = p_id;
    IF NOT FOUND THEN p_error_msg := 'PRO_001:Certificación no encontrada'; RETURN; END IF;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- El payload se recibe como TEXT porque Npgsql/Dapper no convierte parámetros de texto
-- automáticamente a JSONB al ejecutar CALL. Se agrupa antes en el servicio y aquí se hace
-- un único batch transaccional por request.
CREATE OR REPLACE FUNCTION usp_CatalogoCertificaciones_SincronizarCensus(p_items TEXT)
RETURNS TABLE(insertadas INT, actualizadas INT, sin_archivo INT)
LANGUAGE plpgsql
AS $$
DECLARE
    item JSONB;
    v_nombre VARCHAR(250);
    v_nombre_normalizado VARCHAR(250);
    v_file_id VARCHAR(200);
    v_institucion VARCHAR(200);
    v_vigencia VARCHAR(100);
    v_existente BIGINT;
    v_insertadas INT := 0;
    v_actualizadas INT := 0;
    v_sin_archivo INT := 0;
BEGIN
    FOR item IN SELECT value FROM jsonb_array_elements(p_items::JSONB) LOOP
        v_nombre := NULLIF(item->>'nombre', '');
        v_nombre_normalizado := NULLIF(item->>'nombreNormalizado', '');
        IF v_nombre IS NULL OR v_nombre_normalizado IS NULL THEN CONTINUE; END IF;
        v_file_id := NULLIF(item->>'fileIdCensus', '');
        v_institucion := NULLIF(item->>'institucion', '');
        v_vigencia := NULLIF(item->>'vigencia', '');

        SELECT id INTO v_existente FROM catalogo_certificaciones
        WHERE nombre_normalizado = v_nombre_normalizado;
        IF v_existente IS NULL THEN
            INSERT INTO catalogo_certificaciones
                (nombre, nombre_normalizado, file_id_census, institucion, vigencia)
            VALUES (v_nombre, v_nombre_normalizado, v_file_id, v_institucion, v_vigencia);
            v_insertadas := v_insertadas + 1;
        ELSE
            UPDATE catalogo_certificaciones
            SET nombre = v_nombre,
                file_id_census = COALESCE(v_file_id, file_id_census),
                institucion = COALESCE(v_institucion, institucion),
                vigencia = COALESCE(v_vigencia, vigencia),
                activo = TRUE,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = v_existente;
            v_actualizadas := v_actualizadas + 1;
        END IF;
        IF v_file_id IS NULL THEN v_sin_archivo := v_sin_archivo + 1; END IF;
    END LOOP;
    RETURN QUERY SELECT v_insertadas, v_actualizadas, v_sin_archivo;
END;
$$;

-- ── Catálogo de capítulos ─────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION usp_CatalogoCapitulos_Obtener(p_id BIGINT)
RETURNS TABLE(
    id BIGINT, titulo VARCHAR(250), contenido_markdown TEXT, orden INT, activo BOOLEAN,
    created_at TIMESTAMP, updated_at TIMESTAMP
)
LANGUAGE SQL
AS $$
    SELECT c.id, c.titulo, c.contenido_markdown, c.orden, c.activo, c.created_at, c.updated_at
    FROM catalogo_capitulos c WHERE c.id = p_id;
$$;

CREATE OR REPLACE FUNCTION usp_CatalogoCapitulos_Listar(
    p_q VARCHAR(250), p_activo BOOLEAN, p_offset INT, p_limit INT
)
RETURNS TABLE(
    id BIGINT, titulo VARCHAR(250), contenido_markdown TEXT, orden INT, activo BOOLEAN,
    created_at TIMESTAMP, updated_at TIMESTAMP, total_count BIGINT
)
LANGUAGE SQL
AS $$
    SELECT c.id, c.titulo, c.contenido_markdown, c.orden, c.activo, c.created_at, c.updated_at,
           COUNT(*) OVER() AS total_count
    FROM catalogo_capitulos c
    WHERE (p_q IS NULL OR c.titulo ILIKE '%' || p_q || '%')
      AND (p_activo IS NULL OR c.activo = p_activo)
    ORDER BY c.orden, c.id
    LIMIT p_limit OFFSET p_offset;
$$;

CREATE OR REPLACE PROCEDURE usp_CatalogoCapitulos_Insertar(
    p_titulo VARCHAR(250), p_contenido_markdown TEXT, p_orden INT,
    INOUT p_id BIGINT DEFAULT 0, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO catalogo_capitulos (titulo, contenido_markdown, orden)
    VALUES (p_titulo, p_contenido_markdown, p_orden)
    RETURNING id INTO p_id;
    p_error_msg := NULL;
EXCEPTION WHEN unique_violation THEN
    p_error_msg := 'PRO_002:El orden del capítulo ya existe';
WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_CatalogoCapitulos_Actualizar(
    p_id BIGINT, p_titulo VARCHAR(250), p_contenido_markdown TEXT, p_orden INT,
    p_activo BOOLEAN, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE catalogo_capitulos
    SET titulo = p_titulo, contenido_markdown = p_contenido_markdown, orden = p_orden,
        activo = p_activo, updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id;
    IF NOT FOUND THEN p_error_msg := 'PRO_001:Capítulo no encontrado'; RETURN; END IF;
    p_error_msg := NULL;
EXCEPTION WHEN unique_violation THEN
    p_error_msg := 'PRO_002:El orden del capítulo ya existe';
WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_CatalogoCapitulos_Eliminar(
    p_id BIGINT, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE catalogo_capitulos SET activo = FALSE, updated_at = CURRENT_TIMESTAMP WHERE id = p_id;
    IF NOT FOUND THEN p_error_msg := 'PRO_001:Capítulo no encontrado'; RETURN; END IF;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- ── Propuestas (persistencia preparada para Bundle B) ─────────────────────────
CREATE OR REPLACE FUNCTION usp_Propuestas_Listar(
    p_licitacion_id BIGINT, p_estado VARCHAR(20), p_offset INT, p_limit INT
)
RETURNS TABLE(
    id BIGINT, licitacion_id BIGINT, version INT, capitulos_seleccionados JSONB,
    certificaciones_ids JSONB, experiencias_ids JSONB, ruta_archivo VARCHAR(500),
    estado VARCHAR(20), generado_por VARCHAR(200), generado_at TIMESTAMP,
    created_at TIMESTAMP, updated_at TIMESTAMP, total_count BIGINT
)
LANGUAGE SQL
AS $$
    SELECT p.id, p.licitacion_id, p.version, p.capitulos_seleccionados,
           p.certificaciones_ids, p.experiencias_ids, p.ruta_archivo, p.estado,
           p.generado_por, p.generado_at, p.created_at, p.updated_at,
           COUNT(*) OVER() AS total_count
    FROM propuestas p
    WHERE p.licitacion_id = p_licitacion_id
      AND (p_estado IS NULL OR p.estado = p_estado)
    ORDER BY p.version DESC
    LIMIT p_limit OFFSET p_offset;
$$;

CREATE OR REPLACE PROCEDURE usp_Propuestas_Insertar(
    p_licitacion_id BIGINT, p_capitulos_seleccionados TEXT, p_certificaciones_ids TEXT,
    p_experiencias_ids TEXT, p_ruta_archivo VARCHAR(500), p_estado VARCHAR(20),
    p_generado_por VARCHAR(200), p_generado_at TIMESTAMP,
    INOUT p_id BIGINT DEFAULT 0, INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
DECLARE v_version INT;
BEGIN
    SELECT COALESCE(MAX(version), 0) + 1 INTO v_version FROM propuestas WHERE licitacion_id = p_licitacion_id;
    INSERT INTO propuestas
        (licitacion_id, version, capitulos_seleccionados, certificaciones_ids, experiencias_ids,
         ruta_archivo, estado, generado_por, generado_at)
    VALUES (p_licitacion_id, v_version, p_capitulos_seleccionados::JSONB,
            p_certificaciones_ids::JSONB, p_experiencias_ids::JSONB, p_ruta_archivo,
            p_estado, p_generado_por, p_generado_at)
    RETURNING id INTO p_id;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_Propuestas_EstadoActualizar(
    p_id BIGINT, p_estado VARCHAR(20), INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
DECLARE v_estado VARCHAR(20);
BEGIN
    SELECT estado INTO v_estado FROM propuestas WHERE id = p_id;
    IF v_estado IS NULL THEN p_error_msg := 'PRO_001:Propuesta no encontrada'; RETURN; END IF;
    IF NOT ((v_estado = 'generada' AND p_estado IN ('enviada', 'descartada'))
            OR (v_estado = 'enviada' AND p_estado = 'descartada')) THEN
        p_error_msg := 'PRO_008:Transición de estado inválida'; RETURN;
    END IF;
    UPDATE propuestas SET estado = p_estado, updated_at = CURRENT_TIMESTAMP WHERE id = p_id;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- La firma de V144 sólo cambió en la tabla de retorno. PostgreSQL no permite
-- CREATE OR REPLACE con ese cambio, por eso se elimina explícitamente.
DROP FUNCTION IF EXISTS usp_LicitacionesDecision_Obtener(BIGINT);

CREATE OR REPLACE FUNCTION usp_LicitacionesDecision_Obtener(p_licitacion_id BIGINT)
RETURNS TABLE(
    licitacion_id BIGINT,
    decision VARCHAR(20),
    motivo TEXT,
    recomendacion_ia VARCHAR(20),
    score_confianza NUMERIC(4,3),
    decidido_por VARCHAR(200),
    decidido_at TIMESTAMP,
    notificados JSONB,
    notificado_at TIMESTAMP
)
LANGUAGE SQL
AS $$
    SELECT i.licitacion_id, i.decision, i.motivo, i.recomendacion_ia,
           i.score_confianza, i.decidido_por, i.decidido_at,
           i.notificados, i.notificado_at
    FROM licitaciones_interes i
    WHERE i.licitacion_id = p_licitacion_id;
$$;

-- Semilla corporativa canónica PRJ-001. No contiene nombres, emails ni IDs de personas.
INSERT INTO catalogo_capitulos (titulo, contenido_markdown, orden, activo)
VALUES
    ('Carátula', '## TIVIT\n\nPropuesta técnica y comercial.', 1, TRUE),
    ('Declaración de confidencialidad', 'La información de esta propuesta es confidencial y se utilizará únicamente para evaluar la oferta.', 2, TRUE),
    ('Resumen ejecutivo', 'Resumen ejecutivo de la solución propuesta, sus beneficios y alcance.', 3, TRUE),
    ('Certificaciones TIVIT', 'Sección dinámica para certificaciones corporativas y sus documentos asociados.', 4, TRUE),
    ('Experiencias TIVIT', 'Sección dinámica para experiencias corporativas seleccionadas.', 5, TRUE),
    ('Alcance del servicio', 'Descripción del alcance, supuestos, exclusiones y criterios de aceptación.', 6, TRUE),
    ('Organigrama', 'Estructura corporativa y roles del servicio, sin datos personales.', 7, TRUE),
    ('Aportes de las partes', 'Responsabilidades y aportes esperados de TIVIT y del cliente.', 8, TRUE),
    ('Listado de entregables', 'Listado base de entregables, hitos y evidencias del servicio.', 9, TRUE),
    ('Capítulos teóricos', 'Metodología, enfoque técnico, gestión de riesgos y mejora continua.', 10, TRUE)
ON CONFLICT (orden) DO UPDATE SET
    titulo = EXCLUDED.titulo,
    contenido_markdown = EXCLUDED.contenido_markdown,
    activo = TRUE,
    updated_at = CURRENT_TIMESTAMP;
