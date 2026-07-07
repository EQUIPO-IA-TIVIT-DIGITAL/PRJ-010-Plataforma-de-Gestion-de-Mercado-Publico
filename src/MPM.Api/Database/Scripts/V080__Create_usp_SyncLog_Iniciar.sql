CREATE OR REPLACE PROCEDURE usp_SyncLog_Iniciar(
    p_tipo VARCHAR(10),
    OUT p_sync_id BIGINT,
    OUT p_error_msg TEXT
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO sync_log (tipo, estado) VALUES (p_tipo, 'EN_PROGRESO')
    RETURNING id INTO p_sync_id;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := SQLERRM;
END;
$$;
