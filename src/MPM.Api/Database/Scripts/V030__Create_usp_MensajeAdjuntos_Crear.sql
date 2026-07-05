CREATE OR REPLACE PROCEDURE usp_MensajeAdjuntos_Crear(
    IN p_mensaje_id BIGINT,
    IN p_nombre_archivo VARCHAR(500),
    IN p_mime_type VARCHAR(200),
    IN p_tamanio_bytes BIGINT,
    IN p_ruta_storage VARCHAR(1000),
    INOUT p_id BIGINT DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT NULL
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM mensajes WHERE id = p_mensaje_id AND deleted_at IS NULL) THEN
        p_error_msg := 'MSG_006: Mensaje no encontrado';
        RETURN;
    END IF;

    INSERT INTO mensaje_adjuntos (mensaje_id, nombre_archivo, mime_type, tamanio_bytes, ruta_storage)
    VALUES (p_mensaje_id, p_nombre_archivo, p_mime_type, p_tamanio_bytes, p_ruta_storage)
    RETURNING id INTO p_id;

EXCEPTION
    WHEN OTHERS THEN
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$;
