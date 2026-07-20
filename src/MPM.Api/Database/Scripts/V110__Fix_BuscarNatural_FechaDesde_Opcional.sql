-- V110 (029-fix-hallazgos-code-review-competidores-alertas, FR-002): usp_Licitaciones_BuscarNatural
-- y su _Count hardcodeaban p_fecha_desde DATE DEFAULT '2026-01-01' y lo usaban en el WHERE sin
-- el guard "p_fecha_desde IS NULL OR ..." que p_fecha_hasta sí tiene (V107) -- toda búsqueda NL
-- de un período anterior a esa fecha quedaba vacía sin explicación, y LicitacionHandler.cs
-- nunca pasaba el FechaDesde real que ConsultaSemanticaService ya infiere de la consulta.
--
-- Se corrige la función para tratar p_fecha_desde como opcional (mismo patrón que p_fecha_hasta)
-- y se cambia el DEFAULT a NULL -- "sin fecha de inicio" es ahora el comportamiento real por
-- defecto, no un hardcode fijo. El caller (LicitacionHandler.BuscarNaturalAsync) pasa el
-- FechaDesde real inferido, o NULL si no se infirió ninguno.

CREATE OR REPLACE FUNCTION usp_Licitaciones_BuscarNatural(
    p_query TEXT,
    p_page INT DEFAULT 1,
    p_page_size INT DEFAULT 20,
    p_estado SMALLINT DEFAULT NULL,
    p_fecha_desde DATE DEFAULT NULL,
    p_terminos_expandidos TEXT[] DEFAULT NULL,
    p_monto_desde NUMERIC DEFAULT NULL,
    p_monto_hasta NUMERIC DEFAULT NULL,
    p_fecha_hasta DATE DEFAULT NULL
)
RETURNS TABLE(
    id BIGINT,
    codigo_externo VARCHAR(50),
    nombre VARCHAR(500),
    descripcion TEXT,
    organismo VARCHAR(200),
    codigo_estado SMALLINT,
    tipo VARCHAR(30),
    fecha_publicacion TIMESTAMP,
    relevancia REAL
) AS $$
DECLARE
    v_query TSQUERY;
    v_offset INT;
    v_term TEXT;
    v_term_query TSQUERY;
BEGIN
    v_query := plainto_tsquery('spanish', p_query);

    IF v_query IS NULL THEN
        p_query := regexp_replace(p_query, '[^\w\s]', '', 'g');
        IF length(trim(p_query)) = 0 THEN
            RETURN;
        END IF;
        v_query := plainto_tsquery('spanish', p_query);
    END IF;

    IF v_query IS NULL THEN
        RETURN;
    END IF;

    IF p_terminos_expandidos IS NOT NULL THEN
        FOREACH v_term IN ARRAY p_terminos_expandidos LOOP
            v_term_query := plainto_tsquery('spanish', v_term);
            IF v_term_query IS NOT NULL THEN
                v_query := v_query || v_term_query;
            END IF;
        END LOOP;
    END IF;

    v_offset := (p_page - 1) * p_page_size;

    RETURN QUERY
    SELECT l.id, l.codigo_externo, l.nombre,
           substring(l.descripcion, 1, 500)::text AS descripcion,
           l.organismo, l.codigo_estado, l.tipo,
           l.fecha_publicacion,
           ts_rank(l.search_vector, v_query, 32)::real AS relevancia
    FROM licitaciones l
    WHERE l.search_vector @@ v_query
      AND l.deleted_at IS NULL
      AND (p_fecha_desde IS NULL OR l.fecha_publicacion IS NULL OR l.fecha_publicacion >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR l.fecha_publicacion IS NULL OR l.fecha_publicacion <= p_fecha_hasta)
      AND (p_estado IS NULL OR l.codigo_estado = p_estado)
      AND (p_monto_desde IS NULL OR l.monto_estimado >= p_monto_desde)
      AND (p_monto_hasta IS NULL OR l.monto_estimado <= p_monto_hasta)
    ORDER BY relevancia DESC, l.fecha_publicacion DESC
    LIMIT p_page_size
    OFFSET v_offset;
END;
$$ LANGUAGE plpgsql STABLE;


CREATE OR REPLACE FUNCTION usp_Licitaciones_BuscarNatural_Count(
    p_query TEXT,
    p_estado SMALLINT DEFAULT NULL,
    p_fecha_desde DATE DEFAULT NULL,
    p_terminos_expandidos TEXT[] DEFAULT NULL,
    p_monto_desde NUMERIC DEFAULT NULL,
    p_monto_hasta NUMERIC DEFAULT NULL,
    p_fecha_hasta DATE DEFAULT NULL,
    OUT p_total BIGINT
) AS $$
DECLARE
    v_query TSQUERY;
    v_term TEXT;
    v_term_query TSQUERY;
BEGIN
    v_query := plainto_tsquery('spanish', p_query);

    IF v_query IS NULL THEN
        p_total := 0;
        RETURN;
    END IF;

    IF p_terminos_expandidos IS NOT NULL THEN
        FOREACH v_term IN ARRAY p_terminos_expandidos LOOP
            v_term_query := plainto_tsquery('spanish', v_term);
            IF v_term_query IS NOT NULL THEN
                v_query := v_query || v_term_query;
            END IF;
        END LOOP;
    END IF;

    SELECT COUNT(*) INTO p_total
    FROM licitaciones l
    WHERE l.search_vector @@ v_query
      AND l.deleted_at IS NULL
      AND (p_fecha_desde IS NULL OR l.fecha_publicacion IS NULL OR l.fecha_publicacion >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR l.fecha_publicacion IS NULL OR l.fecha_publicacion <= p_fecha_hasta)
      AND (p_estado IS NULL OR l.codigo_estado = p_estado)
      AND (p_monto_desde IS NULL OR l.monto_estimado >= p_monto_desde)
      AND (p_monto_hasta IS NULL OR l.monto_estimado <= p_monto_hasta);
END;
$$ LANGUAGE plpgsql STABLE;
