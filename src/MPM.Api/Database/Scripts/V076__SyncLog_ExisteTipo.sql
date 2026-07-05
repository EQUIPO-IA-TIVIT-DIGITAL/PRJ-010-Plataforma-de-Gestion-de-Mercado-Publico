-- V076: Verifica si existe un sync completado de un tipo dado
-- Usado por el backfill idempotente 2025-2026 del SyncEngineService

CREATE OR REPLACE FUNCTION usp_SyncLog_ExisteTipo(
    p_tipo VARCHAR(10),
    OUT p_existe BOOLEAN
) AS $$
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM sync_log
        WHERE tipo = p_tipo
          AND estado IN ('EXITO', 'PARCIAL')
    ) INTO p_existe;
END;
$$ LANGUAGE plpgsql;
