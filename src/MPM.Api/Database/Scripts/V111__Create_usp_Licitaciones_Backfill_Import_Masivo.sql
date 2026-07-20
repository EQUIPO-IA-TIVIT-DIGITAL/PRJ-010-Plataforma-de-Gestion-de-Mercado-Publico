-- V111 (029-fix-hallazgos-code-review-competidores-alertas, FR-010/US6/QA BUG-003): soporte de
-- datos para el job de backfill de licitaciones del import histórico masivo con tipo genérico
-- ("Licitacion") y/u organismo no recuperado. No se hace el backfill de tipo directamente acá en
-- SQL a propósito -- se reusa ParseTipoDesdeCodigo (MPM.Modules.Licitaciones/Services/ApiMpService.cs)
-- desde el nuevo ImportBackfillService, para que la derivación de tipo tenga una única fuente de
-- verdad compartida con el path de sync normal, en vez de duplicar la lógica en SQL.

CREATE OR REPLACE FUNCTION usp_Licitaciones_ListarParaBackfillTipo(p_limite INT DEFAULT 1000)
RETURNS TABLE(codigo_externo VARCHAR(50), tipo_actual VARCHAR(30))
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT l.codigo_externo, l.tipo
    FROM licitaciones l
    WHERE l.deleted_at IS NULL
      AND (l.tipo IS NULL OR l.tipo = 'Licitacion')
    ORDER BY l.id
    LIMIT p_limite;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_Licitaciones_ActualizarTipoBackfill(
    p_codigo_externo VARCHAR(50),
    p_tipo VARCHAR(30)
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE licitaciones
    SET tipo = p_tipo, updated_at = CURRENT_TIMESTAMP
    WHERE codigo_externo = p_codigo_externo AND deleted_at IS NULL;
END;
$$;

-- Registros candidatos para el backfill de organismo vía API real (mismo trigger que ya usa
-- LicitacionService.ObtenerPorCodigoAsync on-demand: descripcion vacía Y fecha_publicacion nula).
CREATE OR REPLACE FUNCTION usp_Licitaciones_ListarParaBackfillOrganismo(p_limite INT DEFAULT 100)
RETURNS TABLE(codigo_externo VARCHAR(50))
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT l.codigo_externo
    FROM licitaciones l
    WHERE l.deleted_at IS NULL
      AND (l.organismo IS NULL OR l.organismo = '')
      AND l.descripcion IS NULL
      AND l.fecha_publicacion IS NULL
    ORDER BY l.id
    LIMIT p_limite;
END;
$$;
