-- V152: Incluir descripción en índice full-text search_vector
-- Problema: filtro actual solo indexa nombre + codigo_externo (V002)
-- Solución: agregar descripcion con peso B (nombre=A, descripcion=B, codigo=C)
-- Referencia: capacitación 14/08/2026 min 13:12-14:30 (Claudia: "nombre no dice nada, hay que bucear bases")

DROP INDEX IF EXISTS idx_licitaciones_search_vector;

-- NOTA: los paréntesis internos son obligatorios. Sin ellos PostgreSQL rechaza
-- la definición ("syntax error at or near ||") porque cada elemento del índice
-- debe ser una expresión entre paréntesis (mismo patrón que V153). Las BD donde
-- V152 ya figura como aplicada lo saltan sin efecto; V153 la supersede igual.
CREATE INDEX idx_licitaciones_search_vector
ON licitaciones
USING gin (
    (setweight(to_tsvector('spanish', coalesce(nombre,'')), 'A') ||
     setweight(to_tsvector('spanish', coalesce(descripcion,'')), 'B') ||
     setweight(to_tsvector('spanish', coalesce(codigo_externo,'')), 'C'))
)
WHERE deleted_at IS NULL;