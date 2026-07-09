-- QA BUG-015: el auto-vinculado de Telegram (deep-link o Chat ID manual, ambos convergen en
-- usp_AlertasDestinatarios_GuardarChatId) guardaba el chat_id pero nunca marcaba
-- es_account_manager_gobierno = TRUE. Como usp_AlertasDestinatarios_ListarAccountManagers()
-- filtra por esa columna, nadie que se auto-vinculara recibia alertas reales -- sin ningun
-- error visible (notificacionTelegramEnviada=false, notificacionTelegramError=null).

CREATE OR REPLACE FUNCTION usp_AlertasDestinatarios_GuardarChatId(
    p_usuario_id VARCHAR(100),
    p_telegram_chat_id VARCHAR(50)
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO alertas_destinatarios (usuario_id, telegram_chat_id, es_account_manager_gobierno)
    VALUES (p_usuario_id, p_telegram_chat_id, TRUE)
    ON CONFLICT (usuario_id) DO UPDATE
        SET telegram_chat_id = p_telegram_chat_id,
            es_account_manager_gobierno = TRUE,
            updated_at = CURRENT_TIMESTAMP;
END;
$$ LANGUAGE plpgsql;

-- Backfill: usuarios que ya se auto-vincularon antes de este fix y quedaron en FALSE.
-- Limitado a telegram_chat_id no nulo -- es la unica forma de saber que la fila vino de este
-- flujo de auto-servicio y no de una via administrativa distinta que pudiera querer FALSE.
UPDATE alertas_destinatarios
SET es_account_manager_gobierno = TRUE, updated_at = CURRENT_TIMESTAMP
WHERE telegram_chat_id IS NOT NULL
  AND es_account_manager_gobierno = FALSE;
