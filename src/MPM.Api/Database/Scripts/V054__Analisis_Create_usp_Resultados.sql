CREATE OR REPLACE FUNCTION usp_AnalisisResultados_Crear(
    p_workspace_id BIGINT,
    p_documento_id BIGINT,
    p_contenido_json JSONB,
    p_modelo_usado VARCHAR(100),
    p_tokens_entrada INTEGER,
    p_tokens_salida INTEGER,
    OUT p_id BIGINT,
    OUT p_error_msg TEXT
) RETURNS RECORD AS $$
BEGIN
    INSERT INTO analisis_resultados (workspace_id, documento_id, contenido_json, modelo_usado, tokens_entrada, tokens_salida)
    VALUES (p_workspace_id, p_documento_id, p_contenido_json, p_modelo_usado, p_tokens_entrada, p_tokens_salida)
    RETURNING id INTO p_id;

    UPDATE analisis_workspaces
    SET estado = 'completado', updated_at = CURRENT_TIMESTAMP, last_analyzed_at = CURRENT_TIMESTAMP
    WHERE id = p_workspace_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION usp_AnalisisResultados_ObtenerPorWorkspace(
    p_workspace_id BIGINT
) RETURNS TABLE(
    id BIGINT,
    workspace_id BIGINT,
    documento_id BIGINT,
    documento_nombre VARCHAR,
    contenido_json JSONB,
    modelo_usado VARCHAR,
    tokens_entrada INTEGER,
    tokens_salida INTEGER,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT ar.id, ar.workspace_id, ar.documento_id, ad.nombre_archivo,
           ar.contenido_json, ar.modelo_usado, ar.tokens_entrada, ar.tokens_salida, ar.created_at
    FROM analisis_resultados ar
    JOIN analisis_documentos ad ON ad.id = ar.documento_id
    WHERE ar.workspace_id = p_workspace_id
    ORDER BY ar.created_at DESC
    LIMIT 1;
END;
$$ LANGUAGE plpgsql;
