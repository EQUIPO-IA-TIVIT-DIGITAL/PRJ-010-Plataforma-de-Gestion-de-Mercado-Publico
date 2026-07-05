CREATE OR REPLACE FUNCTION usp_MensajeAdjuntos_Obtener(
    p_id BIGINT,
    p_conversacion_id BIGINT,
    p_user_id VARCHAR(100)
)
RETURNS TABLE (
    Id BIGINT,
    MensajeId BIGINT,
    NombreArchivo VARCHAR(500),
    MimeType VARCHAR(200),
    TamanioBytes BIGINT,
    RutaStorage VARCHAR(1000),
    CreatedAt TIMESTAMP
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM conversacion_participantes
        WHERE conversacion_id = p_conversacion_id AND user_id = p_user_id AND left_at IS NULL
    ) THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT
        ma.id,
        ma.mensaje_id,
        ma.nombre_archivo,
        ma.mime_type,
        ma.tamanio_bytes,
        ma.ruta_storage,
        ma.created_at
    FROM mensaje_adjuntos ma
    INNER JOIN mensajes m ON m.id = ma.mensaje_id
    WHERE ma.id = p_id
      AND m.conversacion_id = p_conversacion_id
      AND m.deleted_at IS NULL;
END;
$$;
