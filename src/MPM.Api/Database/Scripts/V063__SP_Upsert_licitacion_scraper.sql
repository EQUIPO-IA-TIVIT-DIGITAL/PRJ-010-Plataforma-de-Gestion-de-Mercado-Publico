-- V063: Stored procedures para upsert de licitaciones desde scraper

-- Upsert licitación desde el scraper (enriquece datos existentes o crea nueva)
CREATE OR REPLACE PROCEDURE usp_Licitacion_UpsertFromScraper(
    p_codigo_externo VARCHAR(50),
    p_nombre VARCHAR(500),
    p_descripcion TEXT DEFAULT NULL,
    p_codigo_estado SMALLINT DEFAULT 6,
    p_tipo VARCHAR(30) DEFAULT NULL,
    p_organismo VARCHAR(200) DEFAULT NULL,
    p_unidad_tecnica VARCHAR(200) DEFAULT NULL,
    p_moneda VARCHAR(5) DEFAULT 'CLP',
    p_monto_estimado DECIMAL(18,2) DEFAULT NULL,
    p_fecha_publicacion TIMESTAMP DEFAULT NULL,
    p_fecha_cierre TIMESTAMP DEFAULT NULL,
    p_fecha_adjudicacion TIMESTAMP DEFAULT NULL,
    p_link VARCHAR(500) DEFAULT NULL,
    p_raw_data JSONB DEFAULT NULL,
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Intentar actualizar si existe
    UPDATE licitaciones SET
        nombre = COALESCE(NULLIF(p_nombre, ''), nombre),
        descripcion = COALESCE(NULLIF(p_descripcion, ''), descripcion),
        codigo_estado = COALESCE(p_codigo_estado, codigo_estado),
        tipo = COALESCE(NULLIF(p_tipo, ''), tipo),
        organismo = COALESCE(NULLIF(p_organismo, ''), organismo),
        unidad_tecnica = COALESCE(NULLIF(p_unidad_tecnica, ''), unidad_tecnica),
        moneda = COALESCE(NULLIF(p_moneda, ''), moneda),
        monto_estimado = COALESCE(p_monto_estimado, monto_estimado),
        fecha_publicacion = COALESCE(p_fecha_publicacion, fecha_publicacion),
        fecha_cierre = COALESCE(p_fecha_cierre, fecha_cierre),
        fecha_adjudicacion = COALESCE(p_fecha_adjudicacion, fecha_adjudicacion),
        link = COALESCE(NULLIF(p_link, ''), link),
        raw_data = CASE WHEN p_raw_data IS NOT NULL THEN p_raw_data ELSE raw_data END,
        updated_at = CURRENT_TIMESTAMP
    WHERE codigo_externo = p_codigo_externo AND deleted_at IS NULL
    RETURNING id INTO p_id;

    -- Si no existe, insertar
    IF p_id IS NULL OR p_id = 0 THEN
        INSERT INTO licitaciones (
            codigo_externo, nombre, descripcion, codigo_estado, tipo,
            organismo, unidad_tecnica, moneda, monto_estimado,
            fecha_publicacion, fecha_cierre, fecha_adjudicacion, link, raw_data
        ) VALUES (
            p_codigo_externo, p_nombre, p_descripcion, p_codigo_estado, p_tipo,
            p_organismo, p_unidad_tecnica, p_moneda, p_monto_estimado,
            p_fecha_publicacion, p_fecha_cierre, p_fecha_adjudicacion, p_link, p_raw_data
        ) RETURNING id INTO p_id;
    END IF;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- Insertar o actualizar adjunto de licitación
CREATE OR REPLACE PROCEDURE usp_Licitaciones_Adjuntos_Upsert(
    p_licitacion_id BIGINT,
    p_tipo VARCHAR(50),
    p_nombre_archivo VARCHAR(500),
    p_ruta_storage TEXT,
    p_nombre_elemento VARCHAR(500) DEFAULT NULL,
    p_ruta_local TEXT DEFAULT NULL,
    p_tamanio_bytes BIGINT DEFAULT NULL,
    p_mime_type VARCHAR(100) DEFAULT NULL,
    p_grid_origen VARCHAR(30) DEFAULT NULL,
    p_acta_descargada BOOLEAN DEFAULT FALSE,
    p_analisis_estado VARCHAR(20) DEFAULT 'pendiente',
    p_analisis_workspace_id BIGINT DEFAULT NULL,
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Verificar si ya existe con la misma ruta
    SELECT id INTO p_id FROM licitaciones_adjuntos
    WHERE licitacion_id = p_licitacion_id AND ruta_storage = p_ruta_storage AND record_status = 1
    LIMIT 1;

    IF p_id IS NOT NULL AND p_id > 0 THEN
        UPDATE licitaciones_adjuntos SET
            tipo = p_tipo,
            nombre_archivo = COALESCE(NULLIF(p_nombre_archivo, ''), nombre_archivo),
            nombre_elemento = COALESCE(NULLIF(p_nombre_elemento, ''), nombre_elemento),
            tamanio_bytes = COALESCE(p_tamanio_bytes, tamanio_bytes),
            mime_type = COALESCE(NULLIF(p_mime_type, ''), mime_type),
            grid_origen = COALESCE(NULLIF(p_grid_origen, ''), grid_origen),
            acta_descargada = COALESCE(p_acta_descargada, acta_descargada),
            analisis_estado = COALESCE(NULLIF(p_analisis_estado, ''), analisis_estado),
            analisis_workspace_id = COALESCE(p_analisis_workspace_id, analisis_workspace_id),
            updated_at = CURRENT_TIMESTAMP
        WHERE id = p_id;
    ELSE
        INSERT INTO licitaciones_adjuntos (
            licitacion_id, tipo, nombre_archivo, nombre_elemento,
            ruta_storage, ruta_local, tamanio_bytes, mime_type,
            grid_origen, acta_descargada, analisis_estado, analisis_workspace_id
        ) VALUES (
            p_licitacion_id, p_tipo, p_nombre_archivo, p_nombre_elemento,
            p_ruta_storage, p_ruta_local, p_tamanio_bytes, p_mime_type,
            p_grid_origen, p_acta_descargada, p_analisis_estado, p_analisis_workspace_id
        ) RETURNING id INTO p_id;
    END IF;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- Actualizar estado de análisis de un adjunto
CREATE OR REPLACE PROCEDURE usp_Licitaciones_Adjuntos_UpdateEstado(
    p_id BIGINT,
    p_analisis_estado VARCHAR(20),
    p_analisis_workspace_id BIGINT DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE licitaciones_adjuntos SET
        analisis_estado = p_analisis_estado,
        analisis_workspace_id = COALESCE(p_analisis_workspace_id, analisis_workspace_id),
        updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- Obtener última sincronización completada del scraper
CREATE OR REPLACE FUNCTION usp_ScraperSync_GetLastCompleted(
    p_tipo VARCHAR DEFAULT 'SCRAPER'
) RETURNS TIMESTAMP
LANGUAGE plpgsql
AS $$
DECLARE
    v_last TIMESTAMP;
BEGIN
    SELECT ejecutado_en INTO v_last
    FROM scraper_sync_log
    WHERE tipo = p_tipo AND estado = 'completado'
    ORDER BY ejecutado_en DESC
    LIMIT 1;
    RETURN COALESCE(v_last, '2000-01-01'::TIMESTAMP);
END;
$$;

-- Iniciar registro de sync
CREATE OR REPLACE PROCEDURE usp_ScraperSync_Start(
    p_tipo VARCHAR DEFAULT 'SCRAPER',
    p_fecha_desde TIMESTAMP DEFAULT NULL,
    p_fecha_hasta TIMESTAMP DEFAULT NULL,
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO scraper_sync_log (tipo, fecha_desde, fecha_hasta, estado)
    VALUES (p_tipo, p_fecha_desde, p_fecha_hasta, 'iniciado')
    RETURNING id INTO p_id;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- Finalizar registro de sync
CREATE OR REPLACE PROCEDURE usp_ScraperSync_End(
    p_id BIGINT,
    p_registros_procesados INT DEFAULT 0,
    p_nuevos INT DEFAULT 0,
    p_actualizados INT DEFAULT 0,
    p_errores INT DEFAULT 0,
    p_detalle_errores JSONB DEFAULT NULL,
    p_total_licitaciones INT DEFAULT 0,
    p_total_con_acta INT DEFAULT 0,
    p_total_sin_acta INT DEFAULT 0,
    p_total_analizados INT DEFAULT 0,
    p_duracion_ms BIGINT DEFAULT NULL,
    p_estado VARCHAR DEFAULT 'completado',
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE scraper_sync_log SET
        registros_procesados = p_registros_procesados,
        nuevos = p_nuevos,
        actualizados = p_actualizados,
        errores = p_errores,
        detalle_errores = p_detalle_errores,
        total_licitaciones = p_total_licitaciones,
        total_con_acta = p_total_con_acta,
        total_sin_acta = p_total_sin_acta,
        total_analizados = p_total_analizados,
        duracion_ms = p_duracion_ms,
        estado = p_estado
    WHERE id = p_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- Verificar si una licitación ya existe por código externo
CREATE OR REPLACE FUNCTION usp_Licitacion_YaExistePorCodigo(
    p_codigo_externo VARCHAR(50)
) RETURNS BIGINT
LANGUAGE plpgsql
AS $$
DECLARE
    v_id BIGINT;
BEGIN
    SELECT id INTO v_id FROM licitaciones
    WHERE codigo_externo = p_codigo_externo AND deleted_at IS NULL
    LIMIT 1;
    RETURN COALESCE(v_id, 0);
END;
$$;