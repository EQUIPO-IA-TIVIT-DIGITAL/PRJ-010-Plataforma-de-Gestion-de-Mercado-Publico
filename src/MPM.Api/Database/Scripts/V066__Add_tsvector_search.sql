-- V066: Indexación full-text search para licitaciones
-- Agrega columna tsvector + trigger automático + GIN index

ALTER TABLE licitaciones ADD COLUMN IF NOT EXISTS search_vector TSVECTOR;

CREATE OR REPLACE FUNCTION fn_licitaciones_search_index() RETURNS trigger AS $$
BEGIN
  NEW.search_vector := to_tsvector('spanish',
    COALESCE(NEW.nombre, '') || ' ' ||
    COALESCE(NEW.descripcion, '') || ' ' ||
    COALESCE(NEW.organismo, '') || ' ' ||
    COALESCE(NEW.codigo_externo, '')
  );
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_licitaciones_search_index ON licitaciones;
CREATE TRIGGER trg_licitaciones_search_index
  BEFORE INSERT OR UPDATE OF nombre, descripcion, organismo, codigo_externo
  ON licitaciones
  FOR EACH ROW EXECUTE FUNCTION fn_licitaciones_search_index();

CREATE INDEX IF NOT EXISTS idx_licitaciones_search_vector
  ON licitaciones USING GIN(search_vector);

-- Backfill: indexar datos existentes desde 2026
UPDATE licitaciones SET search_vector =
  to_tsvector('spanish',
    COALESCE(nombre,'') || ' ' ||
    COALESCE(descripcion,'') || ' ' ||
    COALESCE(organismo,'') || ' ' ||
    COALESCE(codigo_externo,'')
  )
WHERE deleted_at IS NULL
  AND (fecha_publicacion >= '2026-01-01' OR fecha_publicacion IS NULL)
  AND search_vector IS NULL;
