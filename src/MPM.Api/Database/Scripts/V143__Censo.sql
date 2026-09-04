-- V143: Módulo Censo (036-flujo-comercial-ofertas, Fase 2) — catálogo refrescable,
-- cache de expansión (IA solo primera vez), cache de personas por tecnología (TTL 24h),
-- resultados de match y preferencias de usuario (toggle de país). CERO réplica persistente
-- de Census: solo cachés de resultados.

CREATE TABLE IF NOT EXISTS censo_catalogo (
    id BIGSERIAL PRIMARY KEY,
    grupo VARCHAR(100) NOT NULL,
    categoria VARCHAR(150) NOT NULL,
    type_name VARCHAR(200) NOT NULL,
    tecnologia VARCHAR(200) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (type_name, tecnologia)
);

CREATE INDEX IF NOT EXISTS idx_censo_catalogo_type ON censo_catalogo(type_name);
CREATE INDEX IF NOT EXISTS idx_censo_catalogo_tecnologia ON censo_catalogo(tecnologia);

CREATE TABLE IF NOT EXISTS censo_expansiones (
    id BIGSERIAL PRIMARY KEY,
    concepto VARCHAR(200) NOT NULL UNIQUE,
    tecnologias JSONB NOT NULL,
    fuente VARCHAR(20) NOT NULL DEFAULT 'catalogo',  -- catalogo | ia
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS censo_cache_personas (
    id BIGSERIAL PRIMARY KEY,
    tecnologia VARCHAR(200) NOT NULL,
    pais VARCHAR(100) NOT NULL DEFAULT '',
    personas JSONB NOT NULL,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (tecnologia, pais)
);

CREATE INDEX IF NOT EXISTS idx_censo_cache_personas_tecnologia ON censo_cache_personas(tecnologia);

CREATE TABLE IF NOT EXISTS censo_match (
    id BIGSERIAL PRIMARY KEY,
    licitacion_id BIGINT NOT NULL REFERENCES licitaciones(id),
    resultado_json JSONB NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (licitacion_id)
);

CREATE TABLE IF NOT EXISTS censo_preferencias (
    id BIGSERIAL PRIMARY KEY,
    user_id VARCHAR(200) NOT NULL UNIQUE,
    filtrar_pais BOOLEAN NOT NULL DEFAULT FALSE,
    pais VARCHAR(100) NOT NULL DEFAULT 'Chile',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ── SPs ──────────────────────────────────────────────────────────────────────
CREATE OR REPLACE PROCEDURE usp_CensoCatalogo_Upsert(
    p_grupo VARCHAR(100),
    p_categoria VARCHAR(150),
    p_type_name VARCHAR(200),
    p_tecnologia VARCHAR(200),
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO censo_catalogo (grupo, categoria, type_name, tecnologia)
    VALUES (p_grupo, p_categoria, p_type_name, p_tecnologia)
    ON CONFLICT (type_name, tecnologia)
    DO UPDATE SET grupo = EXCLUDED.grupo, categoria = EXCLUDED.categoria;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_CensoCatalogo_Limpiar(
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    TRUNCATE censo_catalogo;
    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE FUNCTION usp_CensoCatalogo_Listar()
RETURNS TABLE(grupo VARCHAR, categoria VARCHAR, type_name VARCHAR, tecnologia VARCHAR) AS $$
BEGIN
    RETURN QUERY SELECT c.grupo, c.categoria, c.type_name, c.tecnologia
    FROM censo_catalogo c ORDER BY c.type_name, c.tecnologia;
END;
$$ LANGUAGE plpgsql;

-- Elimina sobrecargas viejas (JSONB) que CREATE OR REPLACE no reemplaza — sin esto la
-- resolución de sobrecarga con argumentos text no encuentra el procedure (42883).
-- La firma del DROP debe incluir el INOUT p_error_msg (firma de identidad completa).
DROP PROCEDURE IF EXISTS usp_CensoExpansion_Upsert(VARCHAR(200), JSONB, VARCHAR(20), TEXT);
DROP PROCEDURE IF EXISTS usp_CensoCachePersonas_Upsert(VARCHAR(200), VARCHAR(100), JSONB, TEXT);
DROP PROCEDURE IF EXISTS usp_CensoMatch_Guardar(BIGINT, JSONB, TEXT);

CREATE OR REPLACE PROCEDURE usp_CensoExpansion_Upsert(
    p_concepto VARCHAR(200),
    p_tecnologias TEXT,
    p_fuente VARCHAR(20),
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- p_tecnologias llega como TEXT (Dapper no castea a JSONB en CALL): cast explícito.
    INSERT INTO censo_expansiones (concepto, tecnologias, fuente)
    VALUES (p_concepto, p_tecnologias::JSONB, p_fuente)
    ON CONFLICT (concepto)
    DO UPDATE SET tecnologias = EXCLUDED.tecnologias, fuente = EXCLUDED.fuente,
                  updated_at = CURRENT_TIMESTAMP;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE FUNCTION usp_CensoExpansion_Obtener(p_concepto VARCHAR(200))
RETURNS TABLE(concepto VARCHAR, tecnologias JSONB, fuente VARCHAR(20)) AS $$
BEGIN
    RETURN QUERY SELECT e.concepto, e.tecnologias, e.fuente
    FROM censo_expansiones e WHERE e.concepto = p_concepto;
END;
$$ LANGUAGE plpgsql;

-- Cache de personas: obtiene si está fresco (< 24 h), o devuelve vacío para re-consultar.
CREATE OR REPLACE FUNCTION usp_CensoCachePersonas_ObtenerFresco(
    p_tecnologia VARCHAR(200),
    p_pais VARCHAR(100)
)
RETURNS TABLE(tecnologia VARCHAR, pais VARCHAR, personas JSONB, updated_at TIMESTAMP) AS $$
BEGIN
    RETURN QUERY
    SELECT c.tecnologia, c.pais, c.personas, c.updated_at
    FROM censo_cache_personas c
    WHERE c.tecnologia = p_tecnologia AND c.pais = p_pais
      AND c.updated_at > CURRENT_TIMESTAMP - INTERVAL '24 hours';
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE usp_CensoCachePersonas_Upsert(
    p_tecnologia VARCHAR(200),
    p_pais VARCHAR(100),
    p_personas TEXT,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO censo_cache_personas (tecnologia, pais, personas)
    VALUES (p_tecnologia, p_pais, p_personas::JSONB)
    ON CONFLICT (tecnologia, pais)
    DO UPDATE SET personas = EXCLUDED.personas, updated_at = CURRENT_TIMESTAMP;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE PROCEDURE usp_CensoMatch_Guardar(
    p_licitacion_id BIGINT,
    p_resultado_json TEXT,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO censo_match (licitacion_id, resultado_json)
    VALUES (p_licitacion_id, p_resultado_json::JSONB)
    ON CONFLICT (licitacion_id)
    DO UPDATE SET resultado_json = EXCLUDED.resultado_json, updated_at = CURRENT_TIMESTAMP;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE FUNCTION usp_CensoMatch_Obtener(p_licitacion_id BIGINT)
RETURNS TABLE(id BIGINT, resultado_json JSONB, updated_at TIMESTAMP) AS $$
BEGIN
    RETURN QUERY SELECT m.id, m.resultado_json, m.updated_at
    FROM censo_match m WHERE m.licitacion_id = p_licitacion_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE usp_CensoPreferencias_Upsert(
    p_user_id VARCHAR(200),
    p_filtrar_pais BOOLEAN,
    p_pais VARCHAR(100),
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO censo_preferencias (user_id, filtrar_pais, pais)
    VALUES (p_user_id, p_filtrar_pais, p_pais)
    ON CONFLICT (user_id)
    DO UPDATE SET filtrar_pais = EXCLUDED.filtrar_pais, pais = EXCLUDED.pais,
                  updated_at = CURRENT_TIMESTAMP;

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

CREATE OR REPLACE FUNCTION usp_CensoPreferencias_Obtener(p_user_id VARCHAR(200))
RETURNS TABLE(user_id VARCHAR, filtrar_pais BOOLEAN, pais VARCHAR(100)) AS $$
BEGIN
    RETURN QUERY
    SELECT c.user_id, c.filtrar_pais, c.pais
    FROM censo_preferencias c WHERE c.user_id = p_user_id;
END;
$$ LANGUAGE plpgsql;
