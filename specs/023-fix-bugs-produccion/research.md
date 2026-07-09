# Research: Corrección urgente de bugs detectados en producción

## R1 — BUG-014: causa real (corregido tras reproducir en vivo, 2026-07-09)

**Hallazgo inicial (INCORRECTO, descartado)**: se había asumido un mismatch `"directa"` (frontend) vs `'directo'` (CHECK CONSTRAINT de la DB), diagnosticado apurados durante la demo sin inspeccionar el payload real. Al revisar `CrearConversacionModal.tsx` se confirmó que el frontend YA usaba `TIPO_CONVERSACION.DIRECTO`/`GRUPAL` correctamente — esa hipótesis era falsa.

**Causa real confirmada** (reproducida en local, `docker compose`, con logs y `curl` directos): `usp_Conversaciones_Crear` es un `PROCEDURE` (no `FUNCTION`) invocado vía `CALL` desde Dapper. `ConversacionHandler.CrearAsync` agregaba los parámetros `p_asunto` y `p_licitacion_id` sin `dbType` explícito — cuando su valor C# es `null`, Npgsql los envía como parámetro `unknown` sin tipo. Además, `p_participante_ids` se serializaba a JSON como `string` plano sin cast a `jsonb`. Postgres, al resolver la sobrecarga de un `PROCEDURE` invocado con `CALL` (más estricto que una `FUNCTION` con casts implícitos), no encuentra ninguna firma que matchee y lanza `42883: procedure usp_conversaciones_crear(text, unknown, unknown, text, text, bigint, text) does not exist`. El controlador captura esto como una excepción genérica (`SYS_001`) y responde 400.

**Decision**: (1) Especificar `dbType: DbType.String`/`DbType.Int64` explícito para `p_tipo`, `p_asunto`, `p_licitacion_id`, `p_creador_id` en `ConversacionHandler.CrearAsync`. (2) Agregar el cast `::jsonb` explícito a `@p_participante_ids` en el SQL de `MensajeriaStoredProcedures.CrearConversacion`.

**Rationale**: Confirmado en vivo — antes del fix, `POST /api/v1/conversaciones` con un payload perfectamente válido (`{"tipo":"directo","asunto":null,"licitacionId":null,"participanteIds":["2"]}`) fallaba con 400/`42883`; después del fix, mismo payload responde 200 tanto para conversaciones directas como grupales.

**Alternatives considered**: Cambiar `usp_Conversaciones_Crear` de `PROCEDURE` a `FUNCTION` — descartado por ser un cambio de esquema más invasivo y porque el problema real es de tipado del lado del cliente (Dapper/Npgsql), no de la definición del procedure en sí.

## R2 — BUG-015: dónde vive el fix de `es_account_manager_gobierno`

**Decision**: Modificar el stored procedure `usp_AlertasDestinatarios_GuardarChatId` (definido en `V079__Create_Alertas.sql`) vía `CREATE OR REPLACE FUNCTION` en una nueva migración `V096`, agregando `es_account_manager_gobierno = TRUE` tanto al `INSERT` como al `ON CONFLICT DO UPDATE`. En la misma migración, un `UPDATE alertas_destinatarios SET es_account_manager_gobierno = TRUE WHERE telegram_chat_id IS NOT NULL AND es_account_manager_gobierno = FALSE;` para backfill de usuarios ya vinculados.

**Rationale**: Los dos caminos de auto-servicio (`AlertasService.GuardarMiTelegramAsync` y `VincularTelegramPorTokenAsync`) ya convergen en el mismo stored procedure (`handler.GuardarChatIdAsync` → `usp_AlertasDestinatarios_GuardarChatId`), así que un solo cambio de SP cubre ambos flujos sin duplicar lógica en C#. Mantiene el patrón "Stored Procedures First" del proyecto (Principio II).

**Alternatives considered**:
- Setear el flag desde el código C# (`AlertasHandler.GuardarChatIdAsync`) en vez del SP — descartado: rompería el patrón "toda lógica de escritura vive en el stored procedure", y requeriría cambiar la firma del SP igual si se quisiera hacerlo atómico.
- Crear un stored procedure nuevo separado para "habilitar como account manager" — descartado por sobre-ingeniería: no hay ningún caso de uso real hoy donde se quiera guardar un chat_id SIN habilitar la entrega (ver Assumptions del spec — FR-007 solo exige no romper la vía administrativa existente, no crear una nueva).
- No hacer backfill, solo corregir hacia adelante — descartado: el spec (FR-005, SC-003) exige explícitamente que los usuarios ya vinculados (como el usado en la demo del 2026-07-09) queden habilitados sin re-vincular.

## R3 — Alcance del backfill: ¿puede pisar una fila administrativa que intencionalmente tenía el flag en `FALSE`?

**Decision**: El `UPDATE` de backfill se limita a filas con `telegram_chat_id IS NOT NULL` — una fila con `es_account_manager_gobierno = FALSE` y `telegram_chat_id NULL` (creada por un proceso administrativo que aún no vinculó Telegram) no se toca.

**Rationale**: Solo una fila con `telegram_chat_id` no nulo es, por construcción del código actual, el resultado de haber pasado por `usp_AlertasDestinatarios_GuardarChatId` — es decir, por el flujo de auto-servicio que este mismo fix corrige. No existe hoy ningún camino que inserte un `telegram_chat_id` sin que sea vía ese SP.

**Alternatives considered**: Backfill de TODAS las filas sin filtrar — descartado por el riesgo mencionado en el Edge Case del spec (pisar una fila deliberadamente `FALSE` creada por otra vía administrativa que no pasa por este SP).
