-- V068: Fix usp_Notificaciones_Crear parameter types
-- Npgsql 8.x sends C# string as 'text' but the function was declared with
-- jsonb/varchar, causing 42883 "function does not exist" errors.
-- Change to TEXT and cast internally.

DROP FUNCTION IF EXISTS usp_Notificaciones_Crear(text, character varying, text, text, jsonb);

CREATE OR REPLACE FUNCTION usp_Notificaciones_Crear(
    p_usuario_id TEXT,
    p_tipo TEXT,
    p_titulo TEXT,
    p_mensaje TEXT,
    p_metadata TEXT DEFAULT NULL,
    OUT p_id BIGINT,
    OUT p_error_msg TEXT
) AS $$
BEGIN
    p_error_msg := NULL;

    INSERT INTO notificaciones (usuario_id, tipo, titulo, mensaje, metadata)
    VALUES (p_usuario_id, p_tipo, p_titulo, p_mensaje, p_metadata::jsonb)
    RETURNING id INTO p_id;

EXCEPTION
    WHEN OTHERS THEN
        p_id := 0;
        p_error_msg := 'SYS_001: ' || SQLERRM;
END;
$$ LANGUAGE plpgsql;
