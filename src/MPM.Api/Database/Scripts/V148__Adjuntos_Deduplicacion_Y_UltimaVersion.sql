-- V148: Deduplicación definitiva y última versión por archivo en adjuntos de licitación
-- Garantiza que el listado de documentos no muestre duplicados y que el upsert detecte archivos
-- por nombre o ruta aunque existan variaciones de espacios o versiones.

-- 1. Actualizar listado de adjuntos para retornar únicamente la última versión activa de cada archivo
CREATE OR REPLACE FUNCTION usp_Adjuntos_ListarPorLicitacion(
    p_licitacion_id BIGINT
)
RETURNS TABLE(
    id BIGINT,
    licitacion_id BIGINT,
    tipo VARCHAR(50),
    nombre_archivo VARCHAR(500),
    nombre_elemento VARCHAR(500),
    ruta_storage TEXT,
    ruta_local TEXT,
    tamanio_bytes BIGINT,
    mime_type VARCHAR(100),
    acta_descargada BOOLEAN,
    sha256_hash VARCHAR(64),
    fecha_grilla VARCHAR(100),
    version INT,
    descarga_estado VARCHAR(20),
    descarga_error TEXT,
    descarga_iniciada_por VARCHAR(200),
    descarga_iniciada_at TIMESTAMP,
    descarga_fin_at TIMESTAMP,
    created_at TIMESTAMP,
    updated_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT DISTINCT ON (TRIM(a.nombre_archivo))
        a.id, a.licitacion_id, a.tipo, a.nombre_archivo, a.nombre_elemento,
        a.ruta_storage, a.ruta_local, a.tamanio_bytes, a.mime_type,
        a.acta_descargada, a.sha256_hash, a.fecha_grilla, a.version,
        a.descarga_estado, a.descarga_error, a.descarga_iniciada_por,
        a.descarga_iniciada_at, a.descarga_fin_at, a.created_at, a.updated_at
    FROM licitaciones_adjuntos a
    WHERE a.licitacion_id = p_licitacion_id AND a.record_status = 1
    ORDER BY TRIM(a.nombre_archivo), a.version DESC, a.id DESC;
END;
$$ LANGUAGE plpgsql;

-- 2. Actualizar Upsert para buscar coincidencia tanto por ruta_storage como por nombre_archivo
CREATE OR REPLACE PROCEDURE usp_Adjuntos_UpsertConHash(
    p_licitacion_id BIGINT,
    p_tipo VARCHAR(50),
    p_nombre_archivo VARCHAR(500),
    p_ruta_storage TEXT,
    p_nombre_elemento VARCHAR(500),
    p_ruta_local TEXT,
    p_tamanio_bytes BIGINT,
    p_mime_type VARCHAR(100),
    p_es_acta BOOLEAN,
    p_sha256_hash VARCHAR(64),
    p_fecha_grilla VARCHAR(100),
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_version INT DEFAULT 0,
    INOUT p_creado BOOLEAN DEFAULT FALSE,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_existente BIGINT;
    v_hash_actual VARCHAR(64);
    v_version_actual INT;
BEGIN
    SELECT id, sha256_hash, version INTO v_existente, v_hash_actual, v_version_actual
    FROM licitaciones_adjuntos
    WHERE licitacion_id = p_licitacion_id 
      AND (ruta_storage = p_ruta_storage OR TRIM(nombre_archivo) = TRIM(p_nombre_archivo))
      AND record_status = 1
    ORDER BY version DESC
    LIMIT 1;

    IF v_existente IS NOT NULL AND (v_hash_actual IS NULL OR v_hash_actual = p_sha256_hash) THEN
        -- Mismo contenido (o sin hash previo): actualizar metadatos, mantener versión.
        UPDATE licitaciones_adjuntos SET
            tipo = p_tipo,
            nombre_archivo = COALESCE(NULLIF(p_nombre_archivo, ''), nombre_archivo),
            nombre_elemento = COALESCE(NULLIF(p_nombre_elemento, ''), nombre_elemento),
            ruta_storage = COALESCE(NULLIF(p_ruta_storage, ''), ruta_storage),
            ruta_local = COALESCE(p_ruta_local, ruta_local),
            tamanio_bytes = COALESCE(p_tamanio_bytes, tamanio_bytes),
            mime_type = COALESCE(NULLIF(p_mime_type, ''), mime_type),
            acta_descargada = COALESCE(p_es_acta, acta_descargada),
            sha256_hash = COALESCE(NULLIF(p_sha256_hash, ''), sha256_hash),
            fecha_grilla = COALESCE(NULLIF(p_fecha_grilla, ''), fecha_grilla),
            updated_at = CURRENT_TIMESTAMP
        WHERE id = v_existente;

        p_id := v_existente;
        p_version := v_version_actual;
        p_creado := FALSE;
    ELSIF v_existente IS NOT NULL AND v_hash_actual <> p_sha256_hash THEN
        -- Contenido cambió: nueva versión (la anterior queda como historial).
        v_version_actual := v_version_actual + 1;

        INSERT INTO licitaciones_adjuntos (
            licitacion_id, tipo, nombre_archivo, nombre_elemento,
            ruta_storage, ruta_local, tamanio_bytes, mime_type,
            grid_origen, acta_descargada, analisis_estado,
            sha256_hash, fecha_grilla, version, metodo_extraccion
        ) VALUES (
            p_licitacion_id, p_tipo, p_nombre_archivo, p_nombre_elemento,
            p_ruta_storage, p_ruta_local, p_tamanio_bytes, p_mime_type,
            'DWNL_grdId', p_es_acta, 'pendiente',
            p_sha256_hash, p_fecha_grilla, v_version_actual, 'navegador'
        ) RETURNING id INTO p_id;

        p_version := v_version_actual;
        p_creado := TRUE;
    ELSE
        -- Ruta/Archivo nuevo: versión 1.
        INSERT INTO licitaciones_adjuntos (
            licitacion_id, tipo, nombre_archivo, nombre_elemento,
            ruta_storage, ruta_local, tamanio_bytes, mime_type,
            grid_origen, acta_descargada, analisis_estado,
            sha256_hash, fecha_grilla, version, metodo_extraccion
        ) VALUES (
            p_licitacion_id, p_tipo, p_nombre_archivo, p_nombre_elemento,
            p_ruta_storage, p_ruta_local, p_tamanio_bytes, p_mime_type,
            'DWNL_grdId', p_es_acta, 'pendiente',
            p_sha256_hash, p_fecha_grilla, 1, 'navegador'
        ) RETURNING id INTO p_id;

        p_version := 1;
        p_creado := TRUE;
    END IF;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;
