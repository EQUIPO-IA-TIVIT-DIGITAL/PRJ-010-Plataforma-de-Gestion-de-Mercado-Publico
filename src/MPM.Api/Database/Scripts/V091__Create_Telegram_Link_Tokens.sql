-- Vinculación automática de chat_id de Telegram vía deep link + webhook
-- (Fase 6 - Alertas, User Story 5, mejora 2026-07-07: reemplaza el copiar/pegar manual
-- del chat_id por un token de un solo uso embebido en un link https://t.me/<bot>?start=<token>).
-- El bot recibe "/start <token>" en el webhook, que resuelve el token a un usuario_id
-- y guarda el chat_id automáticamente sin intervención manual.

CREATE TABLE IF NOT EXISTS alertas_telegram_link_tokens (
    token VARCHAR(64) PRIMARY KEY,
    usuario_id VARCHAR(100) NOT NULL,
    creado_en TIMESTAMPTZ NOT NULL DEFAULT now(),
    expira_en TIMESTAMPTZ NOT NULL,
    usado BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX IF NOT EXISTS idx_telegram_link_tokens_usuario ON alertas_telegram_link_tokens(usuario_id);

CREATE OR REPLACE FUNCTION usp_TelegramLinkTokens_Crear(
    p_usuario_id VARCHAR(100),
    p_token VARCHAR(64),
    p_ttl_minutos INT DEFAULT 10
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO alertas_telegram_link_tokens (token, usuario_id, expira_en)
    VALUES (p_token, p_usuario_id, now() + (p_ttl_minutos || ' minutes')::INTERVAL);
END;
$$ LANGUAGE plpgsql;

-- Devuelve el usuario_id dueño del token si es válido (no usado, no expirado) y lo marca
-- como usado en la misma operación -- de un solo uso, evita que alguien reintente el link.
CREATE OR REPLACE FUNCTION usp_TelegramLinkTokens_Consumir(
    p_token VARCHAR(64)
)
RETURNS TABLE (p_usuario_id VARCHAR(100)) AS $$
BEGIN
    RETURN QUERY
    UPDATE alertas_telegram_link_tokens t
    SET usado = TRUE
    WHERE t.token = p_token
      AND t.usado = FALSE
      AND t.expira_en > now()
    RETURNING t.usuario_id;
END;
$$ LANGUAGE plpgsql;
