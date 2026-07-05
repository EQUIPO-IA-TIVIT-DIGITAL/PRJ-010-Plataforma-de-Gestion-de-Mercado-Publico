-- V069: Fix usp_SyncLog_Finalizar parameter types
-- Npgsql 8.x sends C# string as 'text' but the procedure was declared with
-- jsonb, causing 42883 "procedure does not exist" errors.
-- Change to TEXT and cast internally.

DROP PROCEDURE IF EXISTS usp_SyncLog_Finalizar(bigint, integer, integer, integer, integer, jsonb, text);

CREATE OR REPLACE PROCEDURE usp_SyncLog_Finalizar(
    p_sync_id BIGINT,
    p_creados INT,
    p_actualizados INT,
    p_eliminados INT,
    p_errores INT,
    p_detalle_errores TEXT DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE sync_log
    SET estado = CASE
        WHEN p_errores > 0 AND p_creados = 0 AND p_actualizados = 0 THEN 'FALLO'
        WHEN p_errores > 0 THEN 'PARCIAL'
        ELSE 'EXITO'
    END,
    registros_procesados = p_creados + p_actualizados + p_eliminados,
    creados = p_creados,
    actualizados = p_actualizados,
    eliminados = p_eliminados,
    errores = p_errores,
    detalle_errores = CASE
        WHEN p_detalle_errores IS NOT NULL THEN p_detalle_errores::jsonb
        ELSE NULL
    END,
    ejecutado_en = CURRENT_TIMESTAMP
    WHERE id = p_sync_id;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := SQLERRM;
END;
$$;
