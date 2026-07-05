-- V077: usp_Notificaciones_Eliminar / EliminarTodas no borraban las notificaciones
-- de sistema (usuario_id = '00000000-...'), aunque usp_Notificaciones_Listar sí las
-- incluye en el listado del usuario. Resultado: "Borrar todas" no eliminaba nada
-- porque casi todas las notificaciones visibles son broadcast, no del usuario.
-- Se alinea el WHERE de ambos SPs con el de Listar.

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
      AND (usuario_id = p_usuario_id OR usuario_id = '00000000-0000-0000-0000-000000000000')
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
    WHERE (usuario_id = p_usuario_id OR usuario_id = '00000000-0000-0000-0000-000000000000')
      AND record_status = 1;

    GET DIAGNOSTICS p_count = ROW_COUNT;

EXCEPTION
    WHEN OTHERS THEN
        p_count := 0;
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$ LANGUAGE plpgsql;
