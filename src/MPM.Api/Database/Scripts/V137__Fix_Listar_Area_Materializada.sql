-- V137: usp_Licitaciones_Listar y usp_Licitaciones_ContarPorEstado usan la columna
-- materializada area_codigos (V136) en vez de evaluar fn_licitacion_area_codigos
-- por fila en runtime -- el filtro de área pasaba de ~3.3s (20k filas) a índice GIN.
-- Se requiere DROP previo: cambia el cuerpo y ambos SPs ya existen con esta firma.

DROP FUNCTION IF EXISTS usp_Licitaciones_Listar(INT, INT, VARCHAR, SMALLINT, VARCHAR, VARCHAR, DATE, DATE, VARCHAR, VARCHAR, SMALLINT, BOOLEAN);
DROP FUNCTION IF EXISTS usp_Licitaciones_ContarPorEstado(SMALLINT, BOOLEAN);

CREATE OR REPLACE FUNCTION usp_Licitaciones_Listar(
    p_page INT DEFAULT 1,
    p_page_size INT DEFAULT 20,
    p_search VARCHAR DEFAULT NULL,
    p_estado SMALLINT DEFAULT NULL,
    p_tipo VARCHAR DEFAULT NULL,
    p_organismo VARCHAR DEFAULT NULL,
    p_fecha_desde DATE DEFAULT NULL,
    p_fecha_hasta DATE DEFAULT NULL,
    p_sort_by VARCHAR DEFAULT 'fecha_publicacion',
    p_sort_dir VARCHAR DEFAULT 'desc',
    p_area SMALLINT DEFAULT NULL,
    p_sin_clasificar BOOLEAN DEFAULT NULL
)
RETURNS TABLE (
    Id BIGINT,
    CodigoExterno VARCHAR,
    Nombre VARCHAR,
    Tipo VARCHAR,
    CodigoEstado SMALLINT,
    EstadoNombre VARCHAR,
    Organismo VARCHAR,
    FechaPublicacion TIMESTAMP,
    FechaCierre TIMESTAMP,
    MontoEstimado DECIMAL,
    Moneda VARCHAR,
    ItemsCount INT,
    TotalCount BIGINT
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_offset INT;
    v_query TSQUERY;
BEGIN
    v_offset := (GREATEST(p_page, 1) - 1) * p_page_size;

    IF p_sort_by IS NULL OR p_sort_by NOT IN ('fecha_publicacion', 'fecha_cierre', 'nombre', 'monto_estimado', 'codigo_externo') THEN
        p_sort_by := 'fecha_publicacion';
    END IF;
    IF p_sort_dir IS NULL OR p_sort_dir NOT IN ('asc', 'desc') THEN
        p_sort_dir := 'desc';
    END IF;

    IF p_search IS NOT NULL AND length(trim(p_search)) > 0 THEN
        v_query := websearch_to_tsquery('spanish', p_search);
    END IF;

    IF p_area IS NOT NULL THEN
        p_sin_clasificar := NULL;
    END IF;

    RETURN QUERY
    SELECT
        l.id AS Id,
        l.codigo_externo AS CodigoExterno,
        l.nombre AS Nombre,
        l.tipo AS Tipo,
        l.codigo_estado AS CodigoEstado,
        e.nombre AS EstadoNombre,
        l.organismo AS Organismo,
        l.fecha_publicacion AS FechaPublicacion,
        l.fecha_cierre AS FechaCierre,
        l.monto_estimado AS MontoEstimado,
        l.moneda AS Moneda,
        (SELECT COUNT(*)::INT FROM licitaciones_items li WHERE li.licitacion_id = l.id) AS ItemsCount,
        COUNT(*) OVER() AS TotalCount
    FROM licitaciones l
    JOIN estados_licitacion e ON e.codigo = l.codigo_estado
    WHERE l.deleted_at IS NULL
      AND (
        p_search IS NULL OR length(trim(p_search)) = 0
        OR (v_query IS NOT NULL AND l.search_vector @@ v_query)
        OR l.codigo_externo ILIKE '%' || p_search || '%'
      )
      AND (p_estado IS NULL OR l.codigo_estado = p_estado)
      AND (p_tipo IS NULL OR l.tipo = p_tipo)
      AND (p_organismo IS NULL OR l.organismo ILIKE '%' || p_organismo || '%')
      AND (p_fecha_desde IS NULL OR l.fecha_publicacion >= p_fecha_desde)
      AND (p_fecha_hasta IS NULL OR l.fecha_publicacion <= (p_fecha_hasta + INTERVAL '1 day'))
      -- V137: usa area_codigos materializada (V136) + índices GIN/btree, en vez de
      -- fn_licitacion_area_codigos(search_vector) evaluada por fila en runtime.
      AND (p_area IS NULL OR p_area = ANY(l.area_codigos))
      AND (p_sin_clasificar IS NOT TRUE OR COALESCE(cardinality(l.area_codigos), 0) = 0)
    ORDER BY
        CASE WHEN p_sort_by = 'fecha_publicacion' AND p_sort_dir = 'asc' THEN l.fecha_publicacion END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'fecha_publicacion' AND p_sort_dir = 'desc' THEN l.fecha_publicacion END DESC NULLS LAST,
        CASE WHEN p_sort_by = 'fecha_cierre' AND p_sort_dir = 'asc' THEN l.fecha_cierre END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'fecha_cierre' AND p_sort_dir = 'desc' THEN l.fecha_cierre END DESC NULLS LAST,
        CASE WHEN p_sort_by = 'nombre' AND p_sort_dir = 'asc' THEN l.nombre END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'nombre' AND p_sort_dir = 'desc' THEN l.nombre END DESC NULLS LAST,
        CASE WHEN p_sort_by = 'monto_estimado' AND p_sort_dir = 'asc' THEN l.monto_estimado END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'monto_estimado' AND p_sort_dir = 'desc' THEN l.monto_estimado END DESC NULLS LAST,
        CASE WHEN p_sort_by = 'codigo_externo' AND p_sort_dir = 'asc' THEN l.codigo_externo END ASC NULLS LAST,
        CASE WHEN p_sort_by = 'codigo_externo' AND p_sort_dir = 'desc' THEN l.codigo_externo END DESC NULLS LAST,
        l.id DESC
    OFFSET v_offset
    LIMIT p_page_size;
END;
$$;

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
        -- V137: columna materializada en vez de función por fila (ver usp_Licitaciones_Listar).
        AND (p_area IS NULL OR p_area = ANY(l.area_codigos))
        AND (p_sin_clasificar IS NOT TRUE OR COALESCE(cardinality(l.area_codigos), 0) = 0)
    WHERE e.codigo IN (5, 6, 7, 8, 15) -- estados reales confirmados en V086; excluye los 1-4 legacy sin uso
    GROUP BY e.codigo, e.nombre
    ORDER BY e.codigo;
END;
$$;
