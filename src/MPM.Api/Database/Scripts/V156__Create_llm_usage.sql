-- V156: Observabilidad LLM - tabla llm_usage + pricing + vista costos diarios + SPs (037-C costos LLM)
-- Scope: 037-C (costos LLM y dashboards). Tabla nueva, sin ALTER de tablas existentes. Idempotente.
-- OBS-R003: etiqueta provider/modelo. OBS-R004: costo via pricing. OBS-R008: try/catch en servicio (no SP).
-- Esta migración es No-lock: solo DDL nuevo, no toca licitaciones (180k) ni analisis (cache).
-- Seeded pricing: gemini-2.5-pro (Vertex AI) y qwen3.7-g4 (OpenAI-compatible) en CLP/1K tokens.

-- ============================================================
-- Tabla llm_usage: una fila por llamada LLM (Gemini o Qwen)
-- ============================================================
CREATE TABLE IF NOT EXISTS llm_usage (
    id BIGSERIAL PRIMARY KEY,
    trace_id VARCHAR(32) NOT NULL,
    provider VARCHAR(20) NOT NULL CHECK (provider IN ('gemini', 'openai', 'vertex', 'qwen', 'anthropic')),
    modelo VARCHAR(50) NOT NULL,
    prompt_tokens INT,
    completion_tokens INT,
    total_tokens INT GENERATED ALWAYS AS (COALESCE(prompt_tokens, 0) + COALESCE(completion_tokens, 0)) STORED,
    latency_ms INT,
    costo_clp NUMERIC(12,2),
    licitacion_id BIGINT NULL REFERENCES licitaciones(id) ON DELETE SET NULL,
    workspace_id BIGINT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Índices para OBS dashboard queries y debugging por TraceId
CREATE INDEX IF NOT EXISTS idx_llm_usage_trace ON llm_usage(trace_id);
CREATE INDEX IF NOT EXISTS idx_llm_usage_provider_modelo ON llm_usage(provider, modelo);
CREATE INDEX IF NOT EXISTS idx_llm_usage_created ON llm_usage(created_at);
CREATE INDEX IF NOT EXISTS idx_llm_usage_licitacion ON llm_usage(licitacion_id) WHERE licitacion_id IS NOT NULL;

-- ============================================================
-- Tabla pricing por modelo (CLP por 1K tokens)
-- ============================================================
CREATE TABLE IF NOT EXISTS llm_model_pricing (
    modelo VARCHAR(50) PRIMARY KEY,
    precio_prompt_1k NUMERIC(10,4) NOT NULL,
    precio_completion_1k NUMERIC(10,4) NOT NULL,
    moneda VARCHAR(3) NOT NULL DEFAULT 'CLP',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Seeds idempotentes (ON CONFLICT DO UPDATE para re-ejecutable sin duplicar)
INSERT INTO llm_model_pricing (modelo, precio_prompt_1k, precio_completion_1k, moneda, updated_at)
VALUES
    ('gemini-2.5-pro', 0.18, 0.60, 'CLP', NOW()),
    ('gemini-1.5-pro', 0.12, 0.36, 'CLP', NOW()),
    ('qwen3.7-g4', 0.10, 0.30, 'CLP', NOW()),
    ('qwen-3-7b', 0.08, 0.24, 'CLP', NOW())
ON CONFLICT (modelo) DO UPDATE SET
    precio_prompt_1k = EXCLUDED.precio_prompt_1k,
    precio_completion_1k = EXCLUDED.precio_completion_1k,
    updated_at = NOW();

-- ============================================================
-- Vista agregado diario por provider/modelo (para dashboard y endpoint)
-- ============================================================
CREATE OR REPLACE VIEW v_llm_costos_diarios AS
SELECT
    date_trunc('day', created_at)::date AS dia,
    provider,
    modelo,
    COUNT(*)::BIGINT AS calls,
    SUM(total_tokens)::BIGINT AS tokens,
    SUM(COALESCE(costo_clp, 0))::NUMERIC(12,2) AS costo
FROM llm_usage
GROUP BY 1, 2, 3;

-- ============================================================
-- SP: usp_LlmUsage_Registrar - inserta fila calculando costo via pricing
-- OBS-R004: costo_clp = (prompt/1000 * precio_prompt_1k) + (completion/1000 * precio_completion_1k)
-- OBS-R008: nunca debe romper el flujo LLM - el caller hace try/catch (servicio), SP solo calcula.
-- Si pricing no existe, costo = 0 (no falla).
-- ============================================================
CREATE OR REPLACE PROCEDURE usp_LlmUsage_Registrar(
    p_trace_id VARCHAR(32),
    p_provider VARCHAR(20),
    p_modelo VARCHAR(50),
    p_prompt_tokens INT,
    p_completion_tokens INT,
    p_latency_ms INT,
    p_licitacion_id BIGINT DEFAULT NULL,
    p_workspace_id BIGINT DEFAULT NULL,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_precio_prompt NUMERIC(10,4);
    v_precio_completion NUMERIC(10,4);
    v_costo NUMERIC(12,2);
BEGIN
    p_error_msg := NULL;

    -- Normalizar trace_id (32 hex chars W3C). Si viene vacío, generar uno corto.
    IF p_trace_id IS NULL OR btrim(p_trace_id) = '' THEN
        p_trace_id := substr(md5(random()::text), 1, 32);
    END IF;

    -- Buscar pricing para el modelo exacto; si no existe, costo 0
    SELECT precio_prompt_1k, precio_completion_1k INTO v_precio_prompt, v_precio_completion
    FROM llm_model_pricing
    WHERE modelo = p_modelo
    LIMIT 1;

    IF v_precio_prompt IS NULL THEN
        v_precio_prompt := 0;
        v_precio_completion := 0;
    END IF;

    v_costo := ROUND(
        (COALESCE(p_prompt_tokens, 0)::NUMERIC / 1000.0 * v_precio_prompt) +
        (COALESCE(p_completion_tokens, 0)::NUMERIC / 1000.0 * v_precio_completion)
    , 2);

    INSERT INTO llm_usage (
        trace_id, provider, modelo, prompt_tokens, completion_tokens, latency_ms, costo_clp, licitacion_id, workspace_id, created_at
    ) VALUES (
        p_trace_id, LOWER(btrim(p_provider)), btrim(p_modelo),
        p_prompt_tokens, p_completion_tokens, p_latency_ms, v_costo,
        p_licitacion_id, p_workspace_id, NOW()
    );

EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- ============================================================
-- Función: usp_LlmCostos_Resumen - agregado para endpoint admin
-- Retorna filas (dia, provider, modelo, calls, tokens, costo) en rango [desde, hasta] inclusive.
-- Si p_desde/p_hasta NULL, los ignora (trae todo / últimos 30 días por defecto en servicio).
-- ============================================================
CREATE OR REPLACE FUNCTION usp_LlmCostos_Resumen(
    p_desde DATE DEFAULT NULL,
    p_hasta DATE DEFAULT NULL
)
RETURNS TABLE(
    dia DATE,
    provider VARCHAR(20),
    modelo VARCHAR(50),
    calls BIGINT,
    tokens BIGINT,
    costo NUMERIC(12,2)
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT
        v.dia,
        v.provider::VARCHAR(20),
        v.modelo::VARCHAR(50),
        v.calls,
        v.tokens,
        v.costo
    FROM v_llm_costos_diarios v
    WHERE (p_desde IS NULL OR v.dia >= p_desde)
      AND (p_hasta IS NULL OR v.dia <= p_hasta)
    ORDER BY v.dia DESC, v.provider, v.modelo;
END;
$$;

-- Verificación manual (ejecutar en psql tras docker compose up):
-- SELECT * FROM llm_usage LIMIT 1;
-- CALL usp_LlmUsage_Registrar('abc123def456abc123def456abc12345', 'gemini', 'gemini-2.5-pro', 100, 200, 1200, NULL, NULL, NULL);
-- SELECT * FROM llm_usage ORDER BY id DESC LIMIT 2;
-- SELECT * FROM v_llm_costos_diarios ORDER BY dia DESC LIMIT 5;
-- SELECT * FROM usp_LlmCostos_Resumen('2026-08-01'::date, NULL::date);
-- SELECT modelo, precio_prompt_1k, precio_completion_1k FROM llm_model_pricing;
