-- V157: Soporte carga manual de pliegos (ADR-015)
-- Permite metodo_extraccion='manual' (además de 'navegador' del scraper)
-- Nuevo SP usp_Adjuntos_UpsertManual que respeta metodo_extraccion pasado por parámetro

-- Mantener el SP existente usp_Adjuntos_UpsertConHash sin romper (wrapper hacia el nuevo con default 'navegador')
CREATE OR REPLACE PROCEDURE usp_Adjuntos_UpsertManual(
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
    p_metodo_extraccion VARCHAR(20),
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
            metodo_extraccion = COALESCE(NULLIF(p_metodo_extraccion, ''), metodo_extraccion),
            descarga_estado = 'completado',
            descarga_fin_at = CURRENT_TIMESTAMP,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = v_existente;

        p_id := v_existente;
        p_version := v_version_actual;
        p_creado := FALSE;
    ELSIF v_existente IS NOT NULL AND v_hash_actual <> p_sha256_hash THEN
        v_version_actual := v_version_actual + 1;

        INSERT INTO licitaciones_adjuntos (
            licitacion_id, tipo, nombre_archivo, nombre_elemento,
            ruta_storage, ruta_local, tamanio_bytes, mime_type,
            grid_origen, acta_descargada, analisis_estado,
            sha256_hash, fecha_grilla, version, metodo_extraccion,
            descarga_estado, descarga_fin_at
        ) VALUES (
            p_licitacion_id, p_tipo, p_nombre_archivo, p_nombre_elemento,
            p_ruta_storage, p_ruta_local, p_tamanio_bytes, p_mime_type,
            'MANUAL', p_es_acta, 'pendiente',
            p_sha256_hash, p_fecha_grilla, v_version_actual, COALESCE(p_metodo_extraccion, 'manual'),
            'completado', CURRENT_TIMESTAMP
        ) RETURNING id INTO p_id;

        p_version := v_version_actual;
        p_creado := TRUE;
    ELSE
        INSERT INTO licitaciones_adjuntos (
            licitacion_id, tipo, nombre_archivo, nombre_elemento,
            ruta_storage, ruta_local, tamanio_bytes, mime_type,
            grid_origen, acta_descargada, analisis_estado,
            sha256_hash, fecha_grilla, version, metodo_extraccion,
            descarga_estado, descarga_fin_at
        ) VALUES (
            p_licitacion_id, p_tipo, p_nombre_archivo, p_nombre_elemento,
            p_ruta_storage, p_ruta_local, p_tamanio_bytes, p_mime_type,
            'MANUAL', p_es_acta, 'pendiente',
            p_sha256_hash, p_fecha_grilla, 1, COALESCE(p_metodo_extraccion, 'manual'),
            'completado', CURRENT_TIMESTAMP
        ) RETURNING id INTO p_id;

        p_version := 1;
        p_creado := TRUE;
    END IF;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- Mantener compatibilidad: usp_Adjuntos_UpsertConHash ahora delega al nuevo con 'navegador'
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
BEGIN
    CALL usp_Adjuntos_UpsertManual(
        p_licitacion_id, p_tipo, p_nombre_archivo, p_ruta_storage, p_nombre_elemento,
        p_ruta_local, p_tamanio_bytes, p_mime_type, p_es_acta, p_sha256_hash, p_fecha_grilla,
        'navegador', p_id, p_version, p_creado, p_error_msg
    );
END;
$$;
