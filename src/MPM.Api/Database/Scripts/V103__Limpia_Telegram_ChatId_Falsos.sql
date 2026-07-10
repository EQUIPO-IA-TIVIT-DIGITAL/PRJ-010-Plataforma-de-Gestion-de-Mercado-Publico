-- Limpia chat_ids de Telegram que son claramente valores de prueba/placeholder, nunca chats
-- reales -- confirmado en vivo el 2026-07-10 que uno de estos generaba "Bad Request: chat not
-- found" en cada "Probar alerta", visible en el toast de la UI (mal aspecto frente a usuarios
-- reales). Solo se tocan valores conocidos como fake; cualquier chat_id real y valido queda
-- intacto. Con telegram_chat_id = NULL, ese destinatario simplemente deja de intentar Telegram
-- (sigue recibiendo por correo si tiene email_alertas configurado) hasta que vuelva a vincular
-- un chat real via "Mis canales de alerta".
UPDATE alertas_destinatarios
SET telegram_chat_id = NULL
WHERE telegram_chat_id IN ('999888777', '123456789', '111111111', '000000000');
