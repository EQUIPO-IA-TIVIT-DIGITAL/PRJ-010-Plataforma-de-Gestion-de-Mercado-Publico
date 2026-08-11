-- 033-migracion-qwen-g4 (US4): configuración persistida del proveedor de IA activo.
-- El switch del super admin (gcloud/qwen) escribe acá; el runtime resuelve con precedencia
-- BD > env (AI:Provider/AI:Endpoint/AI:Model) > default (gemini). No es multi-tenant:
-- es configuración de infraestructura del sistema, no dato de negocio.
-- Auditoría: cada cambio registra quién lo hizo (updated_by_*) y cuándo. Las filas previas
-- quedan como historial (record_status = 'I'); solo hay una fila activa a la vez.

CREATE TABLE IF NOT EXISTS system_ai_provider (
    id BIGSERIAL PRIMARY KEY,
    provider VARCHAR(20) NOT NULL,          -- 'gemini' | 'openai' (Qwen)
    endpoint VARCHAR(500) NULL,             -- base URL del proveedor openai (URL entregada por el equipo)
    model VARCHAR(100) NOT NULL,            -- id del modelo activo (se persiste en analisis.modelo_usado)
    updated_by_user_id BIGINT NOT NULL,    -- usuarios.id (BIGSERIAL) que cambió el proveedor (auditoría)
    updated_by_username VARCHAR(150) NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    record_status CHAR(1) NOT NULL DEFAULT 'A'   -- 'A' activa | 'I' inactiva (historial)
);

-- Solo una fila activa a la vez (el UPSERT de actualización la mantiene).
CREATE UNIQUE INDEX IF NOT EXISTS ux_system_ai_provider_activa
    ON system_ai_provider (record_status)
    WHERE record_status = 'A';

DROP FUNCTION IF EXISTS usp_SystemConfig_ObtenerAiProvider();

CREATE OR REPLACE FUNCTION usp_SystemConfig_ObtenerAiProvider()
RETURNS TABLE(
    p_provider VARCHAR(20),
    p_endpoint VARCHAR(500),
    p_model VARCHAR(100),
    p_updated_by_user_id BIGINT,
    p_updated_by_username VARCHAR(150),
    p_updated_at TIMESTAMPTZ
) AS $$
BEGIN
    RETURN QUERY
    SELECT s.provider, s.endpoint, s.model, s.updated_by_user_id, s.updated_by_username, s.updated_at
    FROM system_ai_provider s
    WHERE s.record_status = 'A'
    ORDER BY s.updated_at DESC
    LIMIT 1;
END;
$$ LANGUAGE plpgsql;

DROP FUNCTION IF EXISTS usp_SystemConfig_ActualizarAiProvider(VARCHAR, VARCHAR, VARCHAR, BIGINT, VARCHAR);

CREATE OR REPLACE FUNCTION usp_SystemConfig_ActualizarAiProvider(
    p_provider VARCHAR(20),
    p_endpoint VARCHAR(500),
    p_model VARCHAR(100),
    p_updated_by_user_id BIGINT,
    p_updated_by_username VARCHAR(150)
)
RETURNS VOID AS $$
BEGIN
    -- La fila activa previa pasa a historial y se inserta la nueva (último cambio gana).
    UPDATE system_ai_provider SET record_status = 'I' WHERE record_status = 'A';
    INSERT INTO system_ai_provider (provider, endpoint, model, updated_by_user_id, updated_by_username)
    VALUES (p_provider, p_endpoint, p_model, p_updated_by_user_id, p_updated_by_username);
END;
$$ LANGUAGE plpgsql;
