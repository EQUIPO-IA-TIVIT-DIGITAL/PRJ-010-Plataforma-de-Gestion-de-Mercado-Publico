-- V136: Materializa la clasificación de área de negocio por licitación.
-- Problema (reportado en prod 2026-08-12, 187k licitaciones): el filtro por área /
-- "sin clasificar" evaluaba fn_licitacion_area_codigos(search_vector) POR FILA en
-- runtime (3.3s en local con 20k filas; ~30s en prod), sin posibilidad de índice.
-- Fix: columna area_codigos SMALLINT[] calculada en el trigger de search_vector
-- (mismo patrón que V066) + índice GIN para p_area = ANY(...).
--
-- NOTA (2026-08-13): el backfill de las filas existentes NO corre en esta migración.
-- El intento inicial de backfill masivo (set-based, 187k filas) crasheó la instancia
-- de Cloud Run (signal 11 / OOM con 512Mi) y superó el startup timeout. El backfill
-- se ejecuta POST-deploy de forma controlada (scripts/backfill-areas.sql): la
-- columna + trigger + índices aplican en segundos y la plataforma arranca al toque;
-- el trigger ya cubre las filas nuevas, y el backfill rellena las existentes.

ALTER TABLE licitaciones ADD COLUMN IF NOT EXISTS area_codigos SMALLINT[];

-- El trigger de search_vector (V066) ahora también mantiene area_codigos, para que
-- cualquier INSERT/UPDATE que cambie nombre/descripcion/organismo/codigo_externo
-- (o search_vector directo, ej. backfills) recalcule la clasificación de área.
CREATE OR REPLACE FUNCTION fn_licitaciones_search_index() RETURNS trigger AS $$
BEGIN
  NEW.search_vector := to_tsvector('spanish',
    COALESCE(NEW.nombre, '') || ' ' ||
    COALESCE(NEW.descripcion, '') || ' ' ||
    COALESCE(NEW.organismo, '') || ' ' ||
    COALESCE(NEW.codigo_externo, '')
  );
  NEW.area_codigos := fn_licitacion_area_codigos(NEW.search_vector);
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_licitaciones_search_index ON licitaciones;
CREATE TRIGGER trg_licitaciones_search_index
  BEFORE INSERT OR UPDATE OF nombre, descripcion, organismo, codigo_externo, search_vector
  ON licitaciones
  FOR EACH ROW EXECUTE FUNCTION fn_licitaciones_search_index();

-- Backfill POST-deploy: NO va aquí (ver nota arriba) — se ejecuta manualmente con
-- scripts/backfill-areas.sql una vez que la plataforma está arriba. Las filas nuevas
-- se clasifican solas vía el trigger.

-- Índice GIN: acelera "p_area = ANY(area_codigos)" en usp_Licitaciones_Listar y
-- usp_Licitaciones_ContarPorEstado (reescritos en V137).
CREATE INDEX IF NOT EXISTS idx_licitaciones_area_codigos
  ON licitaciones USING GIN(area_codigos)
  WHERE deleted_at IS NULL;

-- Índice de expresión para "sin clasificar" (área vacía/NULL): cardinality(∅)=0 no
-- tiene entradas en el GIN, así que este btree parcial cubre el caso sin_clasificar.
CREATE INDEX IF NOT EXISTS idx_licitaciones_sin_area
  ON licitaciones (COALESCE(cardinality(area_codigos), 0))
  WHERE deleted_at IS NULL;
