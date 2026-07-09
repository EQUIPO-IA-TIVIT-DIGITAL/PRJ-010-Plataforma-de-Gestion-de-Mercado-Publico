-- Auditoría de inicios de sesión exitosos (QA BUG-010): antes no quedaba ningún registro de
-- quién inició sesión ni cuándo, imposibilitando medir adopción (pedido de negocio, deadline
-- día 16). Tabla denormalizada a propósito -- sin FK a usuarios, mismo patrón de auditoría
-- histórica usado en otras tablas del proyecto (el evento conserva el email con el que se
-- autenticó ese día, sin acoplarse al ciclo de vida del usuario).

CREATE TABLE IF NOT EXISTS auth_eventos (
    id BIGSERIAL PRIMARY KEY,
    user_id VARCHAR(50) NOT NULL,
    tenant_id VARCHAR(50) NOT NULL,
    email VARCHAR(200) NOT NULL,
    ip_address VARCHAR(45),
    user_agent TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_auth_eventos_user_id_created_at ON auth_eventos(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_auth_eventos_created_at ON auth_eventos(created_at);

CREATE OR REPLACE FUNCTION usp_Auth_RegistrarEvento(
    p_user_id VARCHAR(50),
    p_tenant_id VARCHAR(50),
    p_email VARCHAR(200),
    p_ip_address VARCHAR(45),
    p_user_agent TEXT
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO auth_eventos (user_id, tenant_id, email, ip_address, user_agent)
    VALUES (p_user_id, p_tenant_id, p_email, p_ip_address, p_user_agent);
END;
$$ LANGUAGE plpgsql;
