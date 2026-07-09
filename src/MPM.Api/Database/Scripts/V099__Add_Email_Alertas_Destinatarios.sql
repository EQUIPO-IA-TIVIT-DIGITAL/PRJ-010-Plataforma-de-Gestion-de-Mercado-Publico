-- 024-inteligencia-competencia-alertas / US3: canal de alertas por correo, adicional a Telegram
-- (no lo reemplaza) -- misma tabla que ya modela "a donde le mando las alertas a este usuario".

ALTER TABLE alertas_destinatarios ADD COLUMN IF NOT EXISTS email_alertas VARCHAR(200);

CREATE OR REPLACE FUNCTION usp_AlertasDestinatarios_GuardarEmail(
    p_usuario_id VARCHAR(100),
    p_email_alertas VARCHAR(200)
)
RETURNS VOID AS $$
BEGIN
    -- Mismo criterio que usp_AlertasDestinatarios_GuardarChatId (V096, QA BUG-015): marcar
    -- es_account_manager_gobierno=TRUE al auto-configurar un canal de entrega, para que
    -- usp_AlertasDestinatarios_ListarAccountManagers() lo incluya.
    INSERT INTO alertas_destinatarios (usuario_id, email_alertas, es_account_manager_gobierno)
    VALUES (p_usuario_id, p_email_alertas, TRUE)
    ON CONFLICT (usuario_id) DO UPDATE
        SET email_alertas = p_email_alertas,
            es_account_manager_gobierno = TRUE,
            updated_at = CURRENT_TIMESTAMP;
END;
$$ LANGUAGE plpgsql;

-- usp_AlertasDestinatarios_ListarAccountManagers (V079/V096) debe devolver tambien el email
-- para que AlertasMatchingService pueda intentar el envio por ambos canales. Postgres no
-- permite CREATE OR REPLACE cuando cambia el RETURNS TABLE (2 columnas -> 3, error 42P13:
-- "cannot change return type of existing function") -- hay que DROP explicito primero.
DROP FUNCTION IF EXISTS usp_AlertasDestinatarios_ListarAccountManagers();

CREATE FUNCTION usp_AlertasDestinatarios_ListarAccountManagers()
RETURNS TABLE(p_usuario_id VARCHAR(100), p_telegram_chat_id VARCHAR(50), p_email_alertas VARCHAR(200)) AS $$
BEGIN
    RETURN QUERY
    SELECT d.usuario_id, d.telegram_chat_id, d.email_alertas
    FROM alertas_destinatarios d
    WHERE d.es_account_manager_gobierno = TRUE;
END;
$$ LANGUAGE plpgsql;
