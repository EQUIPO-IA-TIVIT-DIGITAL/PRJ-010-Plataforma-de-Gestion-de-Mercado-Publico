-- V061: Fix usp_AnalisisResultados_Crear parameter types
-- Npgsql 8.x sends C# string as 'text' but the procedure was declared with
-- jsonb/varchar, causing 42883 "procedure does not exist" errors.
-- Change to TEXT and cast internally.

DROP PROCEDURE IF EXISTS usp_AnalisisResultados_Crear(bigint, bigint, jsonb, character varying, integer, integer, bigint, text);

CREATE OR REPLACE PROCEDURE usp_AnalisisResultados_Crear(
    IN p_workspace_id BIGINT,
    IN p_documento_id BIGINT,
    IN p_contenido_json TEXT,
    IN p_modelo_usado TEXT,
    IN p_tokens_entrada INTEGER,
    IN p_tokens_salida INTEGER,
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO analisis_resultados (workspace_id, documento_id, contenido_json, modelo_usado, tokens_entrada, tokens_salida)
    VALUES (p_workspace_id, p_documento_id, p_contenido_json::jsonb, p_modelo_usado, p_tokens_entrada, p_tokens_salida)
    RETURNING id INTO p_id;

    UPDATE analisis_workspaces
    SET estado = 'completado', updated_at = CURRENT_TIMESTAMP, last_analyzed_at = CURRENT_TIMESTAMP
    WHERE id = p_workspace_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;
