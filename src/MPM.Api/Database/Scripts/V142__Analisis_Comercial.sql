-- V142: Análisis comercial de documentos de licitación (zona IA on-demand) con cache por
-- conjunto de documentos (036-flujo-comercial-ofertas, spec docs/api-first/analisis-comercial.md)
--
-- Una fila por (licitacion_id, conjunto_hash): el hash del conjunto de adjuntos (V141) es la
-- clave de cache — si los documentos no cambiaron, el análisis NO se re-paga (sin doble costo IA).

CREATE TABLE IF NOT EXISTS analisis_licitacion_comercial (
    id BIGSERIAL PRIMARY KEY,
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id),
    conjunto_hash VARCHAR(64) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'pendiente',  -- pendiente|analizando|completado|error
    resultado_json JSONB,
    resumen_ejecutivo TEXT,
    go_no_go VARCHAR(20),                             -- strong_go|go|no_go|strong_no_go (recomendación IA)
    score_confianza NUMERIC(4,3),
    modelo_usado VARCHAR(100),
    tokens_entrada INT,
    tokens_salida INT,
    error TEXT,
    creado_por VARCHAR(200),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (licitacion_id, conjunto_hash)
);

CREATE INDEX IF NOT EXISTS idx_analisis_comercial_licitacion ON analisis_licitacion_comercial(licitacion_id);

-- ─────────────────────────────────────────────────────────────────────────────
-- Inicia (o re-inicia) el análisis de un conjunto: inserta si no existe,
-- o la marca 'analizando' si ya existe (reintento). Devuelve si ya existía.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE PROCEDURE usp_AnalisisComercial_Iniciar(
    p_licitacion_id BIGINT,
    p_conjunto_hash VARCHAR(64),
    p_creado_por VARCHAR(200),
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_ya_existia BOOLEAN DEFAULT FALSE,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT id INTO p_id FROM analisis_licitacion_comercial
    WHERE licitacion_id = p_licitacion_id AND conjunto_hash = p_conjunto_hash
    LIMIT 1;

    IF p_id IS NOT NULL AND p_id > 0 THEN
        p_ya_existia := TRUE;
        UPDATE analisis_licitacion_comercial SET
            estado = 'analizando',
            error = NULL,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = p_id;
    ELSE
        INSERT INTO analisis_licitacion_comercial (licitacion_id, conjunto_hash, estado, creado_por)
        VALUES (p_licitacion_id, p_conjunto_hash, 'analizando', p_creado_por)
        RETURNING id INTO p_id;
    END IF;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- ─────────────────────────────────────────────────────────────────────────────
-- Completa el análisis: resultado, modelo, tokens (o error).
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE PROCEDURE usp_AnalisisComercial_Completar(
    p_id BIGINT,
    p_estado VARCHAR(20),
    p_resultado_json TEXT,
    p_resumen_ejecutivo TEXT,
    p_go_no_go VARCHAR(20),
    p_score_confianza NUMERIC(4,3),
    p_modelo_usado VARCHAR(100),
    p_tokens_entrada INT,
    p_tokens_salida INT,
    p_error TEXT,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE analisis_licitacion_comercial SET
        estado = p_estado,
        -- p_resultado_json llega como TEXT (Dapper no castea a JSONB): cast explícito aquí.
        -- El JSON ya fue validado en C# antes de persistir (JsonDocument.Parse).
        resultado_json = COALESCE(NULLIF(p_resultado_json, '')::JSONB, resultado_json),
        resumen_ejecutivo = COALESCE(p_resumen_ejecutivo, resumen_ejecutivo),
        go_no_go = COALESCE(p_go_no_go, go_no_go),
        score_confianza = COALESCE(p_score_confianza, score_confianza),
        modelo_usado = COALESCE(p_modelo_usado, modelo_usado),
        tokens_entrada = COALESCE(p_tokens_entrada, tokens_entrada),
        tokens_salida = COALESCE(p_tokens_salida, tokens_salida),
        error = p_error,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- ─────────────────────────────────────────────────────────────────────────────
-- Último análisis de la licitación (para estado + polling del frontend).
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR REPLACE FUNCTION usp_AnalisisComercial_ObtenerUltimo(
    p_licitacion_id BIGINT
)
RETURNS TABLE(
    id BIGINT,
    licitacion_id BIGINT,
    conjunto_hash VARCHAR(64),
    estado VARCHAR(20),
    resultado_json JSONB,
    resumen_ejecutivo TEXT,
    go_no_go VARCHAR(20),
    score_confianza NUMERIC(4,3),
    modelo_usado VARCHAR(100),
    tokens_entrada INT,
    tokens_salida INT,
    error TEXT,
    creado_por VARCHAR(200),
    created_at TIMESTAMP,
    updated_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        a.id, a.licitacion_id, a.conjunto_hash, a.estado, a.resultado_json,
        a.resumen_ejecutivo, a.go_no_go, a.score_confianza, a.modelo_usado,
        a.tokens_entrada, a.tokens_salida, a.error, a.creado_por,
        a.created_at, a.updated_at
    FROM analisis_licitacion_comercial a
    WHERE a.licitacion_id = p_licitacion_id
    ORDER BY a.created_at DESC
    LIMIT 1;
END;
$$ LANGUAGE plpgsql;
