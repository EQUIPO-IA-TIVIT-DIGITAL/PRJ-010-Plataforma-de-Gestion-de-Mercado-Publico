-- Tabla para almacenar tokens de recuperación de contraseña
CREATE TABLE password_reset_tokens (
    id BIGSERIAL PRIMARY KEY,
    email VARCHAR(255) NOT NULL,
    token VARCHAR(255) NOT NULL UNIQUE,
    expires_at TIMESTAMP NOT NULL,
    used_at TIMESTAMP NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- Índice para búsqueda rápida por token
    CONSTRAINT idx_password_reset_tokens_token UNIQUE (token)
);

-- Índice para limpieza de tokens expirados
CREATE INDEX idx_password_reset_tokens_expires_at ON password_reset_tokens(expires_at);

-- Índice para búsqueda por email
CREATE INDEX idx_password_reset_tokens_email ON password_reset_tokens(email);
