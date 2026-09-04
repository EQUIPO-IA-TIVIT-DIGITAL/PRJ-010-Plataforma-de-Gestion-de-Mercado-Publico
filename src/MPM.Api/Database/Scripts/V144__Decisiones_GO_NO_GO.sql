-- V144: Decisión GO/NO GO formal (036-flujo-comercial-ofertas, Fase 2).
-- Evoluciona licitaciones_interes (V122): la fila de interés pasa a contener la decisión
-- formal del gerente (GO/NO GO), el motivo, la recomendación de la IA y quién/cuándo decidió.
-- La recomendación IA ya vive en analisis_licitacion_comercial (V142); acá se copia al momento
-- de decidir para mantener el snapshot de la decisión.

ALTER TABLE licitaciones_interes
    ADD COLUMN IF NOT EXISTS decision VARCHAR(20),
    ADD COLUMN IF NOT EXISTS motivo TEXT,
    ADD COLUMN IF NOT EXISTS recomendacion_ia VARCHAR(20),
    ADD COLUMN IF NOT EXISTS score_confianza NUMERIC(4,3),
    ADD COLUMN IF NOT EXISTS decidido_por VARCHAR(200),
    ADD COLUMN IF NOT EXISTS decidido_at TIMESTAMP,
    ADD COLUMN IF NOT EXISTS notificados JSONB;

CREATE OR REPLACE PROCEDURE usp_LicitacionesDecision_Registrar(
    p_licitacion_id BIGINT,
    p_decision VARCHAR(20),
    p_motivo TEXT,
    p_recomendacion_ia VARCHAR(20),
    p_score_confianza NUMERIC(4,3),
    p_decidido_por VARCHAR(200),
    INOUT p_id BIGINT DEFAULT 0,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_estado SMALLINT;
BEGIN
    -- Asegura la fila de interés (la decisión vive sobre licitaciones_interes, UK por licitación)
    SELECT id INTO p_id FROM licitaciones_interes
    WHERE licitacion_id = p_licitacion_id LIMIT 1;

    IF p_id IS NULL OR p_id = 0 THEN
        -- DEC-R011: captura el ESTADO REAL de la licitación al crear la fila de interés
        -- (antes estaba hardcodeado a 1 — inconsistente con la spec 031/V122). Si la
        -- licitación no existe o no tiene estado, fallback 1.
        SELECT codigo_estado INTO v_estado FROM licitaciones
        WHERE id = p_licitacion_id AND deleted_at IS NULL LIMIT 1;
        IF v_estado IS NULL THEN
            v_estado := 1;
        END IF;

        INSERT INTO licitaciones_interes (licitacion_id, marcado_por, estado_licitacion_al_marcar)
        VALUES (p_licitacion_id, p_decidido_por, v_estado)
        RETURNING id INTO p_id;
    END IF;

    UPDATE licitaciones_interes SET
        decision = p_decision,
        motivo = p_motivo,
        recomendacion_ia = p_recomendacion_ia,
        score_confianza = p_score_confianza,
        decidido_por = p_decidido_por,
        decidido_at = CURRENT_TIMESTAMP,
        updated_at = CURRENT_TIMESTAMP
    WHERE id = p_id;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE FUNCTION usp_LicitacionesDecision_Obtener(p_licitacion_id BIGINT)
RETURNS TABLE(
    licitacion_id BIGINT,
    decision VARCHAR(20),
    motivo TEXT,
    recomendacion_ia VARCHAR(20),
    score_confianza NUMERIC(4,3),
    decidido_por VARCHAR(200),
    decidido_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT i.licitacion_id, i.decision, i.motivo, i.recomendacion_ia,
           i.score_confianza, i.decidido_por, i.decidido_at
    FROM licitaciones_interes i
    WHERE i.licitacion_id = p_licitacion_id;
END;
$$ LANGUAGE plpgsql;
