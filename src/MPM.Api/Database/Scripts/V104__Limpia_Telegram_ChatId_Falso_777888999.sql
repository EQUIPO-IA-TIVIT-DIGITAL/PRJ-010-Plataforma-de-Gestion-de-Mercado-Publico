-- V103 limpio valores placeholder conocidos pero no el real -- confirmado en vivo via el log de
-- diagnostico agregado en TelegramNotificationService: el chat_id invalido que seguia generando
-- "Bad Request: chat not found" en cada "Probar alerta" es '777888999', otro valor de prueba
-- nunca vinculado a un chat real de Telegram.
UPDATE alertas_destinatarios
SET telegram_chat_id = NULL
WHERE telegram_chat_id = '777888999';
