-- V146: objetos de generación/versionado del Bundle B.
-- V145 ya creó tablas y catálogos. Este parche no duplica datos: corrige únicamente
-- los objetos que necesitan conocer la versión asignada y serializar generaciones
-- concurrentes por licitación.

CREATE OR REPLACE PROCEDURE usp_Propuestas_Generar(
    p_licitacion_id BIGINT,
    p_capitulos_json TEXT,
    p_certificaciones_json TEXT,
    p_experiencias_json TEXT,
    p_ruta_archivo VARCHAR(500),
    p_generado_por VARCHAR(200),
    INOUT p_version INT DEFAULT 0,
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_version INT;
BEGIN
    -- Bloqueo transaccional por licitación: max+1 y el INSERT son una sola operación
    -- frente a dos requests simultáneos. La UK sigue siendo la última barrera.
    PERFORM pg_advisory_xact_lock(p_licitacion_id);
    SELECT COALESCE(MAX(version), 0) + 1
      INTO v_version
      FROM propuestas
     WHERE licitacion_id = p_licitacion_id;

    INSERT INTO propuestas (
        licitacion_id, version, capitulos_seleccionados, certificaciones_ids,
        experiencias_ids, ruta_archivo, estado, generado_por, generado_at
    )
    VALUES (
        p_licitacion_id, v_version,
        COALESCE(NULLIF(p_capitulos_json, '')::JSONB, '[]'::JSONB),
        COALESCE(NULLIF(p_certificaciones_json, '')::JSONB, '[]'::JSONB),
        COALESCE(NULLIF(p_experiencias_json, '')::JSONB, '[]'::JSONB),
        p_ruta_archivo, 'generada', p_generado_por, CURRENT_TIMESTAMP
    )
    RETURNING id INTO p_id;

    p_version := v_version;
    p_error_msg := NULL;
EXCEPTION
    WHEN unique_violation THEN
        p_error_msg := 'PRO_002:No se pudo asignar una versión única';
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE FUNCTION usp_Propuestas_Obtener(p_id BIGINT)
RETURNS TABLE(
    id BIGINT, licitacion_id BIGINT, version INT, capitulos_seleccionados JSONB,
    certificaciones_ids JSONB, experiencias_ids JSONB, ruta_archivo VARCHAR(500),
    estado VARCHAR(20), generado_por VARCHAR(200), generado_at TIMESTAMP,
    created_at TIMESTAMP, updated_at TIMESTAMP
)
LANGUAGE SQL
AS $$
    SELECT p.id, p.licitacion_id, p.version, p.capitulos_seleccionados,
           p.certificaciones_ids, p.experiencias_ids, p.ruta_archivo, p.estado,
           p.generado_por, p.generado_at, p.created_at, p.updated_at
      FROM propuestas p
     WHERE p.id = p_id;
$$;

CREATE OR REPLACE PROCEDURE usp_Propuestas_ActualizarEstado(
    p_id BIGINT, p_estado VARCHAR(20), INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_estado VARCHAR(20);
BEGIN
    SELECT estado INTO v_estado FROM propuestas WHERE id = p_id;
    IF v_estado IS NULL THEN
        p_error_msg := 'PRO_001:Propuesta no encontrada';
        RETURN;
    END IF;
    IF NOT ((v_estado = 'generada' AND p_estado IN ('enviada', 'descartada'))
            OR (v_estado = 'enviada' AND p_estado = 'descartada')) THEN
        p_error_msg := 'PRO_008:Transición de estado inválida';
        RETURN;
    END IF;
    UPDATE propuestas SET estado = p_estado, updated_at = CURRENT_TIMESTAMP WHERE id = p_id;
    p_error_msg := NULL;
EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;
