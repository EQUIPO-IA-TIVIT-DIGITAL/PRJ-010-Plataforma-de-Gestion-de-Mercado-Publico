-- V158: Preferencias de Usuario — Monto Mínimo por Defecto (Track 1 Feature B, T3)
-- Replica patron censo_preferencias (V143): tabla dedicada + SP Obtener/Upsert + ruta /usuarios/me/preferencias-licitaciones
-- Spec: docs/api-first/preferencias-usuario.md — tabla preferencias_usuario (user_id PK, monto_minimo NUMERIC(18,2) CHECK >=0)

CREATE TABLE IF NOT EXISTS preferencias_usuario (
    user_id VARCHAR(200) PRIMARY KEY,
    monto_minimo NUMERIC(18,2) CHECK (monto_minimo IS NULL OR monto_minimo >= 0),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ── SPs ──────────────────────────────────────────────────────────────────────

-- Obtener preferencia del usuario (retorna 0 o 1 fila, patron idéntico a usp_CensoPreferencias_Obtener)
CREATE OR REPLACE FUNCTION usp_PreferenciasUsuario_Obtener(p_user_id VARCHAR(200))
RETURNS TABLE(user_id VARCHAR, monto_minimo NUMERIC, updated_at TIMESTAMPTZ) AS $$
BEGIN
    RETURN QUERY
    SELECT p.user_id, p.monto_minimo, p.updated_at
    FROM preferencias_usuario p WHERE p.user_id = p_user_id;
END;
$$ LANGUAGE plpgsql;

-- Upsert idempotente: INSERT ... ON CONFLICT DO UPDATE (mismo patron usp_CensoPreferencias_Upsert)
-- p_monto_minimo = NULL => borra preferencia (sin umbral).
-- Valida negativo y rango en SP además del CHECK — setea p_error_msg con prefijo VAL_001.
CREATE OR REPLACE PROCEDURE usp_PreferenciasUsuario_Upsert(
    p_user_id VARCHAR(200),
    p_monto_minimo NUMERIC,
    INOUT p_error_msg TEXT DEFAULT ''
)
LANGUAGE plpgsql
AS $$
BEGIN
    p_error_msg := NULL;

    IF p_user_id IS NULL OR btrim(p_user_id) = '' THEN
        p_error_msg := 'VAL_001:user_id requerido';
        RETURN;
    END IF;

    IF p_monto_minimo IS NOT NULL AND p_monto_minimo < 0 THEN
        p_error_msg := 'VAL_001:montoMinimo no puede ser negativo';
        RETURN;
    END IF;

    IF p_monto_minimo IS NOT NULL AND p_monto_minimo > 999999999999.99 THEN
        p_error_msg := 'VAL_001:montoMinimo fuera de rango';
        RETURN;
    END IF;

    INSERT INTO preferencias_usuario (user_id, monto_minimo, updated_at)
    VALUES (p_user_id, p_monto_minimo, NOW())
    ON CONFLICT (user_id)
    DO UPDATE SET monto_minimo = EXCLUDED.monto_minimo,
                  updated_at = NOW();

    p_error_msg := NULL;
EXCEPTION WHEN OTHERS THEN
    p_error_msg := 'SYS_001:' || SQLERRM;
END;
$$;

-- Verificacion manual (psql tras docker compose up):
-- SELECT * FROM usp_PreferenciasUsuario_Obtener('user-test');              -- 0 filas sin preferencia
-- CALL usp_PreferenciasUsuario_Upsert('user-test', 50000000, NULL);
-- SELECT * FROM usp_PreferenciasUsuario_Obtener('user-test');              -- 50000000
-- CALL usp_PreferenciasUsuario_Upsert('user-test', NULL, NULL);            -- borra (NULL)
-- SELECT * FROM usp_PreferenciasUsuario_Obtener('user-test');              -- 50000000 IS NULL
-- CALL usp_PreferenciasUsuario_Upsert('user-test', -1, NULL);              -- p_error_msg VAL_001
-- -- upsert repetido no duplica filas (PK user_id)
