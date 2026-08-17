-- V147: contrato de avisos GO/NO GO.
-- Bundle C (036-flujo-comercial-ofertas). Re-ejecutable y compatible con V145/V146.

-- El endpoint necesita el id de la fila de decisión para construir la ruta de aviso.
-- Se conserva la firma de entrada y se enriquece el resultado de la función.
DROP FUNCTION IF EXISTS usp_LicitacionesDecision_Obtener(BIGINT);

CREATE OR REPLACE FUNCTION usp_LicitacionesDecision_Obtener(p_licitacion_id BIGINT)
RETURNS TABLE(
    id BIGINT,
    licitacion_id BIGINT,
    decision VARCHAR(20),
    motivo TEXT,
    recomendacion_ia VARCHAR(20),
    score_confianza NUMERIC(4,3),
    decidido_por VARCHAR(200),
    decidido_at TIMESTAMP,
    notificados JSONB,
    notificado_at TIMESTAMP
)
LANGUAGE SQL
AS $$
    SELECT i.id, i.licitacion_id, i.decision, i.motivo, i.recomendacion_ia,
           i.score_confianza, i.decidido_por, i.decidido_at,
           i.notificados, i.notificado_at
      FROM licitaciones_interes i
     WHERE i.licitacion_id = p_licitacion_id;
$$;

-- La persistencia se ejecuta sólo después de que todos los avisos in-app del lote
-- fueron creados. Nunca hace broadcast: guarda exactamente el JSON recibido.
CREATE OR REPLACE PROCEDURE usp_LicitacionesDecision_ActualizarNotificados(
    p_id BIGINT,
    p_notificados_json JSONB,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_actualizadas INT;
BEGIN
    UPDATE licitaciones_interes
       SET notificados = p_notificados_json,
           notificado_at = CURRENT_TIMESTAMP,
           updated_at = CURRENT_TIMESTAMP
     WHERE id = p_id;

    GET DIAGNOSTICS v_actualizadas = ROW_COUNT;
    IF v_actualizadas = 0 THEN
        p_error_msg := 'PRO_011:Decisión no encontrada';
        RETURN;
    END IF;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;
