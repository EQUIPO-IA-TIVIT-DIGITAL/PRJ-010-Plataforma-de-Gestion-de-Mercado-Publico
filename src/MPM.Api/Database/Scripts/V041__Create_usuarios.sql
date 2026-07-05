-- Tabla de usuarios del sistema
CREATE TABLE usuarios (
    id BIGSERIAL PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    nombre VARCHAR(200) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    roles TEXT[] NOT NULL DEFAULT '{}',
    tenant_id VARCHAR(100),
    tenant_nombre VARCHAR(200),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    ultimo_login TIMESTAMP NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP NULL
);

-- Índices
CREATE INDEX idx_usuarios_email ON usuarios(email) WHERE deleted_at IS NULL;
CREATE INDEX idx_usuarios_tenant ON usuarios(tenant_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_usuarios_activo ON usuarios(activo) WHERE deleted_at IS NULL;

-- Trigger para actualizar updated_at
CREATE OR REPLACE FUNCTION fn_update_usuarios_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_usuarios_updated_at
    BEFORE UPDATE ON usuarios
    FOR EACH ROW
    EXECUTE FUNCTION fn_update_usuarios_updated_at();
