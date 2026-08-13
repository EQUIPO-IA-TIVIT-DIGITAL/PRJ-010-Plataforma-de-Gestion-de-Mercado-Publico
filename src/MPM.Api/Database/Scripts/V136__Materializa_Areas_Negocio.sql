-- V136: Materializa la clasificación de área de negocio por licitación.
-- Problema (reportado en prod 2026-08-12, 187k licitaciones): el filtro por área /
-- "sin clasificar" evaluaba fn_licitacion_area_codigos(search_vector) POR FILA en
-- runtime (3.3s en local con 20k filas; ~30s en prod), sin posibilidad de índice.
-- Fix: columna area_codigos SMALLINT[] calculada en el trigger de search_vector
-- (mismo patrón que V066) + backfill one-shot + índice GIN para p_area = ANY(...).

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

-- Backfill one-shot: clasifica las filas existentes (search_vector ya poblado por
-- V066/V093). Version set-based (no funcion por fila): precompila los tsquery de las
-- keywords UNA vez y matchea con JOIN sobre el GIN de search_vector -- el intento
-- inicial por fila supero el startup timeout de Cloud Run en prod (187k filas, 30s).
-- Corre en lotes de 20k para no mantener un bloqueo largo; es idempotente (WHERE
-- area_codigos IS NULL), asi que un arranque interrumpido retoma donde quedo.
DO $$
DECLARE
    v_lote INT := 1;
    v_actualizados INT := 0;
BEGIN
    LOOP
        WITH kw AS (
            SELECT an.codigo, plainto_tsquery('spanish', palabra) AS q
            FROM areas_negocio an, unnest(an.palabras_clave) AS palabra
        ),
        match AS (
            SELECT lic.id, array_agg(DISTINCT k.codigo ORDER BY k.codigo) AS codigos
            FROM licitaciones lic
            JOIN kw k ON lic.search_vector @@ k.q
            WHERE lic.deleted_at IS NULL
              AND lic.area_codigos IS NULL
            GROUP BY lic.id
            LIMIT 20000
        )
        UPDATE licitaciones l
        SET area_codigos = m.codigos
        FROM match m
        WHERE l.id = m.id;

        GET DIAGNOSTICS v_actualizados = ROW_COUNT;
        EXIT WHEN v_actualizados = 0;
        RAISE NOTICE 'Backfill areas lote %: % filas', v_lote, v_actualizados;
        v_lote := v_lote + 1;
    END LOOP;
END $$;

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
