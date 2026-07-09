# Quickstart: Validación de la corrección de BUG-014 y BUG-015

## Prerrequisitos

- `docker compose up --build` corriendo local, o el ambiente de producción (`tivit-cu010`) ya desplegado con los fixes.
- Un usuario de prueba autenticado y otro usuario válido del mismo tenant como participante.
- Un bot de Telegram real vinculable (mismo `TELEGRAM_BOT_TOKEN`/`TELEGRAM_BOT_USERNAME` que ya está configurado).

## US1 — BUG-014: crear conversación nueva

1. Iniciar sesión, ir a `/mensajes`, click en "Nueva".
2. Elegir tipo "Directa (1 a 1)", buscar y seleccionar un participante válido.
3. Click en "Crear".
4. **Esperado**: la conversación se crea (200, no 400), aparece en la lista, y se puede abrir y enviar un mensaje.
5. Repetir con tipo "Grupal" y 2+ participantes — mismo resultado esperado.

## US2 — BUG-015: Telegram habilitado tras auto-vinculación

1. Con un usuario que NUNCA vinculó Telegram, ir a `/alertas`, abrir "Mi Telegram".
2. Click en "Conectar con Telegram", presionar "Iniciar" en el chat real de Telegram.
3. Crear una alerta con `notificarTelegram=true` y una keyword que matchee una licitación real existente.
4. Usar el botón "Probar" sobre esa licitación.
5. **Esperado**: la respuesta de `POST /api/v1/alertas/{id}/probar` trae `notificacionTelegramEnviada: true`, y el mensaje llega al chat de Telegram real.
6. Repetir el paso 1-5 usando el campo manual de Chat ID en vez del deep-link — mismo resultado esperado.

## US2b — Backfill de usuarios ya vinculados

1. Antes del fix: confirmar en la base (`SELECT es_account_manager_gobierno FROM alertas_destinatarios WHERE telegram_chat_id IS NOT NULL;`) que hay filas en `FALSE`.
2. Aplicar la migración `V096`.
3. **Esperado**: todas esas filas quedan en `TRUE` sin que el usuario tenga que volver a vincular nada.

## Verificación automatizable

Agregar al script de verificación en vivo (`specs/022-qa-fixes-preproduccion/verify-live.sh` o equivalente para este spec) un chequeo que:
- Llama `POST /api/v1/conversaciones` con `{"tipo":"directo","participanteIds":[...]}` y confirma 200.
- Llama `POST /api/v1/alertas/{id}/mi-telegram` seguido de una consulta a `alertas_destinatarios` confirmando `es_account_manager_gobierno = TRUE`.
