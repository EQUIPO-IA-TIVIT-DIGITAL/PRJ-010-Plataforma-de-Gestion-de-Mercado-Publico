-- V120: US2 (spec 031) — estadísticas de licitaciones por estado con drill-down.
-- LEFT JOIN desde estados_licitacion para que los 5 estados reales (5/6/7/8/15,
-- ver V086) aparezcan siempre, incluso con cantidad 0. Reutiliza fn_licitacion_area_codigos
-- (V118) con el mismo criterio de "area tiene prioridad sobre sin_clasificar" que
-- usp_Licitaciones_Listar (V119).

CREATE OR REPLACE FUNCTION usp_Licitaciones_ContarPorEstado(
    p_area SMALLINT DEFAULT NULL,
    p_sin_clasificar BOOLEAN DEFAULT NULL
)
RETURNS TABLE (
    CodigoEstado SMALLINT,
    NombreEstado VARCHAR,
    Cantidad INT
)
LANGUAGE plpgsql
AS $$
BEGIN
    IF p_area IS NOT NULL THEN
        p_sin_clasificar := NULL;
    END IF;

    RETURN QUERY
    SELECT
        e.codigo AS CodigoEstado,
        e.nombre AS NombreEstado,
        COUNT(l.id)::INT AS Cantidad
    FROM estados_licitacion e
    LEFT JOIN licitaciones l
        ON l.codigo_estado = e.codigo
        AND l.deleted_at IS NULL
        AND (p_area IS NULL OR p_area = ANY(fn_licitacion_area_codigos(l.search_vector)))
        AND (p_sin_clasificar IS NOT TRUE OR fn_licitacion_area_codigos(l.search_vector) = '{}')
    WHERE e.codigo IN (5, 6, 7, 8, 15) -- estados reales confirmados en V086; excluye los 1-4 legacy sin uso
    GROUP BY e.codigo, e.nombre
    ORDER BY e.codigo;
END;
$$;
