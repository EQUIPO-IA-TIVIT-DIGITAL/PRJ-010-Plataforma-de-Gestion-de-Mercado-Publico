-- V075: Stored procedures para eliminar notificaciones (individual y todas)
-- Sigue el patrón del módulo: aislamiento por usuario_id y borrado lógico via record_status

CREATE OR REPLACE FUNCTION usp_Notificaciones_Eliminar(
    p_id BIGINT,
    p_usuario_id TEXT,
    OUT p_error_msg TEXT
) AS $$
BEGIN
    p_error_msg := NULL;

    UPDATE notificaciones
    SET record_status = 0
    WHERE id = p_id
      AND usuario_id = p_usuario_id
      AND record_status = 1;

    IF NOT FOUND THEN
        p_error_msg := 'NOT_001: Notificacion no encontrada';
    END IF;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_Notificaciones_EliminarTodas(
    p_usuario_id TEXT,
    OUT p_count INT,
    OUT p_error_msg TEXT
) AS $$
BEGIN
    p_error_msg := NULL;

    UPDATE notificaciones
    SET record_status = 0
    WHERE usuario_id = p_usuario_id
      AND record_status = 1;

    GET DIAGNOSTICS p_count = ROW_COUNT;

EXCEPTION
    WHEN OTHERS THEN
        p_count := 0;
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$ LANGUAGE plpgsql;
