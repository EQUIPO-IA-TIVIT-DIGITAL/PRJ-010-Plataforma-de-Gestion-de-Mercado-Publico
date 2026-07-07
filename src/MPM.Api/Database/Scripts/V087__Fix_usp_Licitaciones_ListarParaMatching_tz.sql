-- Mismo patron de bug que V069/V084/V085: SyncEngineService llama a este handler con
-- DateTime.UtcNow (Kind=Utc), que Npgsql envia como TIMESTAMPTZ, pero la funcion V079 declara
-- el parametro como TIMESTAMP (sin zona horaria) -> "function ... does not exist" (42883).
-- Nunca se detecto antes porque el ciclo de sync (que es el unico que la invoca, justo
-- despues de cada sync exitoso) nunca habia terminado con exito hasta corregir V080-V086.
DROP FUNCTION IF EXISTS usp_Licitaciones_ListarParaMatching(TIMESTAMP);

CREATE OR REPLACE FUNCTION usp_Licitaciones_ListarParaMatching(
    p_fecha_desde TIMESTAMPTZ
)
RETURNS TABLE(
    p_id BIGINT, p_codigo_externo VARCHAR(50), p_nombre VARCHAR(500), p_descripcion TEXT,
    p_monto_estimado DECIMAL(18,2), p_tipo VARCHAR(30), p_organismo VARCHAR(200)
) AS $$
BEGIN
    RETURN QUERY
    SELECT l.id, l.codigo_externo, l.nombre, l.descripcion, l.monto_estimado, l.tipo, l.organismo
    FROM licitaciones l
    WHERE l.fecha_publicacion >= p_fecha_desde::TIMESTAMP AND l.deleted_at IS NULL;
END;
$$ LANGUAGE plpgsql;
