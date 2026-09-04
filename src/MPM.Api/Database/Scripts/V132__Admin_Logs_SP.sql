-- V132: Lectura unificada de logs/auditoría para la pantalla de administración.
-- Unifica en una sola función los 5 orígenes de actividad que hoy se escriben
-- pero no se pueden consultar por API:
--   auth_eventos        (inicios de sesión)
--   sync_log            (ciclos de sincronización de licitaciones)
--   scraper_sync_log    (corridas del scraper)
--   extraccion_documentos_log (extracción de adjuntos/actas)
--   system_ai_provider  (historial de cambios del proveedor de IA)
-- Cada origen normaliza a (id, tipo, fecha, estado, detalle legible, extra JSON).

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
    SELECT e.id, 'auth', e.created_at::TIMESTAMP, 'exito',
           'Inicio de sesión de ' || e.email,
           jsonb_build_object('email', e.email, 'ip', e.ip_address, 'user_agent', e.user_agent)::TEXT
    FROM auth_eventos e
    WHERE (p_tipo IS NULL OR p_tipo = 'auth')
      AND (p_desde IS NULL OR e.created_at >= p_desde)
      AND (p_hasta IS NULL OR e.created_at <= p_hasta)
      AND (p_estado IS NULL OR p_estado = 'exito')

    UNION ALL

    -- 2) Ciclos de sincronización (sync_log)
    SELECT s.id, 'sync', s.ejecutado_en, s.estado,
           'Sincronización de licitaciones (' || s.tipo || '): ' || s.registros_procesados || ' registros procesados',
           jsonb_build_object(
               'tipo', s.tipo, 'registros_procesados', s.registros_procesados,
               'creados', s.creados, 'actualizados', s.actualizados,
               'eliminados', s.eliminados, 'errores', s.errores
           )::TEXT
    FROM sync_log s
    WHERE (p_tipo IS NULL OR p_tipo = 'sync')
      AND (p_desde IS NULL OR s.ejecutado_en >= p_desde)
      AND (p_hasta IS NULL OR s.ejecutado_en <= p_hasta)
      AND (p_estado IS NULL OR s.estado = p_estado)

    UNION ALL

    -- 3) Corridas del scraper (scraper_sync_log)
    SELECT sc.id, 'scraper', sc.ejecutado_en, sc.estado,
           'Scraper: ' || COALESCE(sc.total_licitaciones, 0) || ' licitaciones procesadas',
           jsonb_build_object(
               'tipo', sc.tipo, 'nuevos', sc.nuevos, 'actualizados', sc.actualizados,
               'errores', sc.errores, 'total_licitaciones', sc.total_licitaciones,
               'total_con_acta', sc.total_con_acta, 'total_sin_acta', sc.total_sin_acta,
               'total_analizados', sc.total_analizados, 'duracion_ms', sc.duracion_ms
           )::TEXT
    FROM scraper_sync_log sc
    WHERE (p_tipo IS NULL OR p_tipo = 'scraper')
      AND (p_desde IS NULL OR sc.ejecutado_en >= p_desde)
      AND (p_hasta IS NULL OR sc.ejecutado_en <= p_hasta)
      AND (p_estado IS NULL OR sc.estado = p_estado)

    UNION ALL

    -- 4) Extracción de documentos (extraccion_documentos_log)
    SELECT x.id, 'extraccion', x.ejecutado_en, x.estado,
           'Extracción de documentos de licitación ' || x.licitacion_id || ' (' || x.metodo || '): ' || x.documentos_obtenidos || ' documentos',
           jsonb_build_object(
               'licitacion_id', x.licitacion_id, 'metodo', x.metodo,
               'documentos_obtenidos', x.documentos_obtenidos, 'acta_obtenida', x.acta_obtenida,
               'es_fallback', x.es_fallback, 'error', x.error, 'duracion_ms', x.duracion_ms
           )::TEXT
    FROM extraccion_documentos_log x
    WHERE (p_tipo IS NULL OR p_tipo = 'extraccion')
      AND (p_desde IS NULL OR x.ejecutado_en >= p_desde)
      AND (p_hasta IS NULL OR x.ejecutado_en <= p_hasta)
      AND (p_estado IS NULL OR x.estado = p_estado)

    UNION ALL

    -- 5) Historial de cambios del proveedor de IA (system_ai_provider)
    SELECT a.id, 'ai_provider', a.updated_at::TIMESTAMP,
           CASE WHEN a.record_status = 'A' THEN 'activo' ELSE 'historial' END,
           'Proveedor de IA cambiado a ' || a.provider || ' (' || a.model || ') por ' || a.updated_by_username,
           jsonb_build_object(
               'provider', a.provider, 'model', a.model, 'endpoint', a.endpoint,
               'updated_by_username', a.updated_by_username
           )::TEXT
    FROM system_ai_provider a
    WHERE (p_tipo IS NULL OR p_tipo = 'ai_provider')
      AND (p_desde IS NULL OR a.updated_at >= p_desde)
      AND (p_hasta IS NULL OR a.updated_at <= p_hasta)
      AND (p_estado IS NULL OR (CASE WHEN a.record_status = 'A' THEN 'activo' ELSE 'historial' END) = p_estado)

    ORDER BY fecha DESC
    LIMIT p_limite;
END;
$$ LANGUAGE plpgsql;
