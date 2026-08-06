-- V126: usp_Licitaciones_Listar no tenía desempate determinístico en el ORDER BY --
-- confirmado en vivo que hay lotes de sync con miles de filas compartiendo exactamente
-- el mismo fecha_publicacion (ej. 1424 filas con el mismo timestamp al segundo), lo que
-- hace que Postgres pueda devolver un orden distinto entre dos llamadas idénticas
-- (rompe la paginación: la misma fila puede aparecer en dos páginas, o ninguna).
-- Se agrega l.id DESC como desempate final en cada rama de ordenamiento -- estable y
-- barato (ya es la PK con índice).

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
      AND (p_area IS NULL OR p_area = ANY(fn_licitacion_area_codigos(l.search_vector)))
      AND (p_sin_clasificar IS NOT TRUE OR fn_licitacion_area_codigos(l.search_vector) = '{}')
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
