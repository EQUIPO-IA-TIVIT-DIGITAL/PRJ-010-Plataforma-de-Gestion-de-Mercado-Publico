# Quickstart: Validación de Fase 6 — Alertas Inteligentes

**Spec**: [spec.md](./spec.md) | **Contracts**: [contracts/alertas-api.md](./contracts/alertas-api.md)

## Prerrequisitos

- `GEMINI_API_KEY` configurado (ya existe, reutilizado de `MPM.Modules.Analisis`)
- `Telegram:BotToken` configurado (nuevo — crear un bot vía [@BotFather](https://t.me/botfather) en Telegram, obtener el token)
- Al menos un usuario con `alertas_destinatarios.telegram_chat_id` completado (ver procedimiento manual abajo)
- Al menos una licitación real ya sincronizada en la base para poder usar el endpoint de prueba

## Procedimiento manual para obtener un `telegram_chat_id` (primera vez)

1. El usuario busca el bot en Telegram (nombre configurado en BotFather) y le escribe cualquier mensaje (p. ej. "hola").
2. Un desarrollador consulta `https://api.telegram.org/bot{TOKEN}/getUpdates` y ubica el `chat.id` del mensaje recibido.
3. Se guarda ese `chat_id` en `alertas_destinatarios` para ese usuario (vía SP o directamente, hasta que exista UI para esto).

## Escenario 1 — Crear alerta y ver sinónimos generados (User Story 1, 3)

1. `POST /api/v1/alertas` con `{"keyword": "SOC"}`.
2. **Pasa si**: la respuesta incluye `sinonimosIa` con al menos 5 términos relacionados (p. ej. "centro de operaciones de seguridad").

## Escenario 2 — Matching por sinónimo dispara la alerta (User Story 3)

1. Insertar (o esperar a que llegue por sync) una licitación cuyo nombre/descripción mencione solo "centro de operaciones de seguridad" (sin la sigla "SOC").
2. Correr el ciclo de matching (parte del sync).
3. **Pasa si**: se crea un registro en `alertas_disparadas` con `termino_match = "centro de operaciones de seguridad"`, no con la keyword literal.

## Escenario 3 — Resumen enriquecido con campos no determinados (User Story 4)

1. Disparar una alerta sobre una licitación recién sincronizada, sin análisis de documentos todavía.
2. **Pasa si**: `resumen_enriquecido` tiene los campos que sí se pueden inferir de metadatos (ej. presupuesto si el monto está en la licitación) y `null` explícito en los que no (ej. forma de pago) — nunca un valor inventado.

## Escenario 4 — Notificación por Telegram no bloquea el in-app (User Story 5, escenario 2)

1. Configurar `Telegram:BotToken` con un valor inválido a propósito.
2. Disparar una alerta con `notificarTelegram=true`.
3. **Pasa si**: la notificación in-app se crea igual, `notificacion_telegram_enviada=false` y `notificacion_telegram_error` tiene el detalle del fallo — el flujo completo no lanza excepción hacia arriba.

## Escenario 5 — Disparar alerta de prueba para demo (User Story 5, escenario 3)

```bash
curl -X POST /api/v1/alertas/1/probar -d '{"licitacionId": 12345}'
```

**Pasa si**: se genera una notificación in-app y (si Telegram está configurado) un mensaje de Telegram, ambos marcando que es de prueba (`es_prueba=true` en `alertas_disparadas`), usando datos reales de la licitación 12345 — no datos inventados.

## Escenario 6 — Deduplicación (User Story 1, escenario 3)

1. Crear 2 reglas que coincidan con la misma licitación.
2. Correr el ciclo de matching.
3. **Pasa si**: el usuario recibe **una sola** notificación in-app mencionando ambas reglas, no dos notificaciones separadas.
