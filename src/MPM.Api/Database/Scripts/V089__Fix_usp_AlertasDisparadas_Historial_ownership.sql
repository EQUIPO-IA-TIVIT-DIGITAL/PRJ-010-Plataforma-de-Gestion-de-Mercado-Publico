-- T041 (003-fase6-alertas-keywords): usp_AlertasDisparadas_Historial (V079) no validaba que la
-- regla perteneciera al usuario que consulta el historial -- se agrega p_usuario_id y se
-- valida contra alertas_reglas.usuario_id, mismo patron que usp_Alertas_Editar/Eliminar.
DROP FUNCTION IF EXISTS usp_AlertasDisparadas_Historial(BIGINT, INT, INT);

CREATE OR REPLACE FUNCTION usp_AlertasDisparadas_Historial(
    p_regla_id BIGINT,
    p_usuario_id VARCHAR(100),
    p_page INT,
    p_page_size INT
)
RETURNS TABLE(
    p_id BIGINT, p_licitacion_id BIGINT, p_termino_match VARCHAR(200),
    p_resumen_enriquecido JSONB, p_es_prueba BOOLEAN, p_disparada_en TIMESTAMP,
    p_total_count BIGINT
) AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM alertas_reglas r
        WHERE r.id = p_regla_id AND r.usuario_id = p_usuario_id AND r.record_status = 1
    ) THEN
        RETURN;
    END IF;

    RETURN QUERY
    SELECT d.id, d.licitacion_id, d.termino_match, d.resumen_enriquecido, d.es_prueba, d.disparada_en,
           COUNT(*) OVER() AS total_count
    FROM alertas_disparadas d
    WHERE d.regla_id = p_regla_id
    ORDER BY d.disparada_en DESC
    LIMIT p_page_size OFFSET (p_page - 1) * p_page_size;
END;
$$ LANGUAGE plpgsql;
