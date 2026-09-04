-- V134: Fix de usp_Admin_ListarLogs (V132) — ORDER BY inválido en UNION ALL.
-- En un UNION, los nombres de columna resultantes salen del PRIMER SELECT; los literales
-- ('auth', 'exito') y expresiones sin alias no producen nombres tipo/fecha/estado/detalle,
-- por lo que "ORDER BY fecha" fallaba con "invalid UNION/INTERSECT/EXCEPT ORDER BY clause".
-- Fix: alias explícito (AS tipo/fecha/estado/detalle/extra) en el primer SELECT.
-- Se requiere DROP previo (cambia el plan del RETURN QUERY; CREATE OR REPLACE no aplica
-- cuando el cuerpo recompilado falla en analyze).

DROP FUNCTION IF EXISTS usp_Admin_ListarLogs(VARCHAR, TIMESTAMP, TIMESTAMP, VARCHAR, INT);

CREATE OR REPLACE FUNCTION usp_Admin_ListarLogs(
    p_tipo VARCHAR(20) DEFAULT NULL,      -- 'auth' | 'sync' | 'scraper' | 'extraccion' | 'ai_provider' | NULL (todos)
    p_desde TIMESTAMP DEFAULT NULL,
    p_hasta TIMESTAMP DEFAULT NULL,
    p_estado VARCHAR(20) DEFAULT NULL,    -- 'exito' | 'fallo' | 'parcial' | 'en_progreso' | 'activo' | 'historial'...
    p_limite INT DEFAULT 100
)
RETURNS TABLE(
    id BIGINT,
    tipo VARCHAR(20),
    fecha TIMESTAMP,
    estado VARCHAR(20),
    detalle TEXT,
    extra TEXT
) AS $$
BEGIN
    RETURN QUERY

    -- 1) Inicios de sesión (auth_eventos)
    SELECT e.id AS id, 'auth' AS tipo, e.created_at::TIMESTAMP AS fecha, 'exito' AS estado,
           'Inicio de sesión de ' || e.email AS detalle,
           jsonb_build_object('email', e.email, 'ip', e.ip_address, 'user_agent', e.user_agent)::TEXT AS extra
    FROM auth_eventos e
    WHERE (p_tipo IS NULL OR p_tipo = 'auth')
      AND (p_desde IS NULL OR e.created_at >= p_desde)
      AND (p_hasta IS NULL OR e.created_at <= p_hasta)
      AND (p_estado IS NULL OR p_estado = 'exito')

    UNION ALL

    -- 2) Ciclos de sincronización (sync_log)
    SELECT s.id AS id, 'sync' AS tipo, s.ejecutado_en AS fecha, s.estado AS estado,
           'Sincronización de licitaciones (' || s.tipo || '): ' || s.registros_procesados || ' registros procesados' AS detalle,
           jsonb_build_object(
               'tipo', s.tipo, 'registros_procesados', s.registros_procesados,
               'creados', s.creados, 'actualizados', s.actualizados,
               'eliminados', s.eliminados, 'errores', s.errores
           )::TEXT AS extra
    FROM sync_log s
    WHERE (p_tipo IS NULL OR p_tipo = 'sync')
      AND (p_desde IS NULL OR s.ejecutado_en >= p_desde)
      AND (p_hasta IS NULL OR s.ejecutado_en <= p_hasta)
      AND (p_estado IS NULL OR s.estado = p_estado)

    UNION ALL

    -- 3) Corridas del scraper (scraper_sync_log)
    SELECT sc.id AS id, 'scraper' AS tipo, sc.ejecutado_en AS fecha, sc.estado AS estado,
           'Scraper: ' || COALESCE(sc.total_licitaciones, 0) || ' licitaciones procesadas' AS detalle,
           jsonb_build_object(
               'tipo', sc.tipo, 'nuevos', sc.nuevos, 'actualizados', sc.actualizados,
               'errores', sc.errores, 'total_licitaciones', sc.total_licitaciones,
               'total_con_acta', sc.total_con_acta, 'total_sin_acta', sc.total_sin_acta,
               'total_analizados', sc.total_analizados, 'duracion_ms', sc.duracion_ms
           )::TEXT AS extra
    FROM scraper_sync_log sc
    WHERE (p_tipo IS NULL OR p_tipo = 'scraper')
      AND (p_desde IS NULL OR sc.ejecutado_en >= p_desde)
      AND (p_hasta IS NULL OR sc.ejecutado_en <= p_hasta)
      AND (p_estado IS NULL OR sc.estado = p_estado)

    UNION ALL

    -- 4) Extracción de documentos (extraccion_documentos_log)
    SELECT x.id AS id, 'extraccion' AS tipo, x.ejecutado_en AS fecha, x.estado AS estado,
           'Extracción de documentos de licitación ' || x.licitacion_id || ' (' || x.metodo || '): ' || x.documentos_obtenidos || ' documentos' AS detalle,
           jsonb_build_object(
               'licitacion_id', x.licitacion_id, 'metodo', x.metodo,
               'documentos_obtenidos', x.documentos_obtenidos, 'acta_obtenida', x.acta_obtenida,
               'es_fallback', x.es_fallback, 'error', x.error, 'duracion_ms', x.duracion_ms
           )::TEXT AS extra
    FROM extraccion_documentos_log x
    WHERE (p_tipo IS NULL OR p_tipo = 'extraccion')
      AND (p_desde IS NULL OR x.ejecutado_en >= p_desde)
      AND (p_hasta IS NULL OR x.ejecutado_en <= p_hasta)
      AND (p_estado IS NULL OR x.estado = p_estado)

    UNION ALL

    -- 5) Historial de cambios del proveedor de IA (system_ai_provider)
    SELECT a.id AS id, 'ai_provider' AS tipo, a.updated_at::TIMESTAMP AS fecha,
           CASE WHEN a.record_status = 'A' THEN 'activo' ELSE 'historial' END AS estado,
           'Proveedor de IA cambiado a ' || a.provider || ' (' || a.model || ') por ' || a.updated_by_username AS detalle,
           jsonb_build_object(
               'provider', a.provider, 'model', a.model, 'endpoint', a.endpoint,
               'updated_by_username', a.updated_by_username
           )::TEXT AS extra
    FROM system_ai_provider a
    WHERE (p_tipo IS NULL OR p_tipo = 'ai_provider')
      AND (p_desde IS NULL OR a.updated_at >= p_desde)
      AND (p_hasta IS NULL OR a.updated_at <= p_hasta)
      AND (p_estado IS NULL OR (CASE WHEN a.record_status = 'A' THEN 'activo' ELSE 'historial' END) = p_estado)

    ORDER BY fecha DESC
    LIMIT p_limite;
END;
$$ LANGUAGE plpgsql;
