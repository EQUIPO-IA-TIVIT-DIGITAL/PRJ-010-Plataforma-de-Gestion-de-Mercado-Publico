-- V159: CM Resumen API Cache — Track2 ligero mserv (ADR-016 opción B sin zip)
-- Fuente: https://mserv-datos-abiertos.chilecompra.cl/v1/organismSupplier/modality/{year}/{rut}
-- Solo cache agregada Convenio Marco (idModalidad=5), sin planillas zip, sin Windows-1252
-- Spec original ingesta-datos-abiertos.md pivotado a API JSON (ver ADR-016 § Track 2 pivot)

CREATE TABLE IF NOT EXISTS cm_resumen_api_cache (
  anio SMALLINT NOT NULL,
  rut VARCHAR(20) NOT NULL,
  amount_clp BIGINT NOT NULL DEFAULT 0,
  payload_json JSONB NOT NULL,
  actualizado_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (anio, rut)
);

CREATE INDEX IF NOT EXISTS idx_cm_resumen_api_cache_rut ON cm_resumen_api_cache(rut);

-- SP Upsert — PK compuesta (anio, rut) corrige D-001/D-002: un anio puede tener N ruts
CREATE OR REPLACE PROCEDURE usp_CmResumenApi_Upsert(
  p_anio INT,
  p_rut VARCHAR(20),
  p_amount_clp BIGINT,
  p_payload_json JSONB
)
LANGUAGE plpgsql AS $$
BEGIN
  INSERT INTO cm_resumen_api_cache (anio, rut, amount_clp, payload_json, actualizado_at)
  VALUES (p_anio::SMALLINT, p_rut, p_amount_clp, p_payload_json, NOW())
  ON CONFLICT (anio, rut) DO UPDATE SET
    amount_clp = EXCLUDED.amount_clp,
    payload_json = EXCLUDED.payload_json,
    actualizado_at = NOW();
END; $$;

-- Obtener por año (single)
CREATE OR REPLACE FUNCTION usp_CmResumenApi_Obtener(p_anio INT)
RETURNS TABLE(anio SMALLINT, rut VARCHAR, amount_clp BIGINT, payload_json JSONB, actualizado_at TIMESTAMPTZ) AS $$
BEGIN
  RETURN QUERY SELECT c.anio, c.rut, c.amount_clp, c.payload_json, c.actualizado_at FROM cm_resumen_api_cache c WHERE c.anio = p_anio;
END; $$ LANGUAGE plpgsql;

-- Obtener rango por rut (para dashboard YoY / histórico)
CREATE OR REPLACE FUNCTION usp_CmResumenApi_ObtenerRango(p_rut VARCHAR, p_anio_desde INT, p_anio_hasta INT)
RETURNS TABLE(anio SMALLINT, rut VARCHAR, amount_clp BIGINT, payload_json JSONB, actualizado_at TIMESTAMPTZ) AS $$
BEGIN
  RETURN QUERY SELECT c.anio, c.rut, c.amount_clp, c.payload_json, c.actualizado_at FROM cm_resumen_api_cache c WHERE c.rut = p_rut AND c.anio BETWEEN p_anio_desde AND p_anio_hasta ORDER BY c.anio;
END; $$ LANGUAGE plpgsql;

-- View simple (lectura directa sin SP)
CREATE OR REPLACE VIEW vw_cm_resumen_anual AS SELECT anio, rut, amount_clp, payload_json, actualizado_at FROM cm_resumen_api_cache;
