---
description: "Task list for Corrección urgente de bugs detectados en producción (023-fix-bugs-produccion)"
---

# Tasks: Corrección urgente de bugs detectados en producción (Mensajería y Alertas)

**Input**: Design documents from `specs/023-fix-bugs-produccion/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

## Phase 1: Setup

- [X] T001 Confirmar que `V096` sigue siendo el siguiente número de migración libre en `src/MPM.Api/Database/Scripts/` (correr `ls` y comparar contra `CLAUDE.md`) antes de crear el archivo. **Confirmado**: V093/V094/V095 eran los más altos, V096 libre.

## Phase 2: Foundational

*Ninguna tarea foundational — ambos bugs son independientes entre sí (uno frontend/mensajería, otro backend/alertas) y no comparten código ni schema.*

---

## Phase 3: User Story 1 — Crear una conversación nueva en Mensajería (P1) — BUG-014

**Goal**: Que crear una conversación directa o grupal desde la UI de Mensajes funcione siempre, usando el valor de `tipo` que la base de datos realmente acepta.

**Independent Test**: Iniciar sesión, ir a `/mensajes`, crear una conversación directa con otro usuario del tenant, confirmar 200 y que aparece en la lista.

- [X] T002 [P] [US1] ~~Reemplazar literal "directa" por TIPO_CONVERSACION.DIRECTO~~ — **DESCARTADO tras reproducir en local**: el frontend (`CrearConversacionModal.tsx`) ya usaba `'directo'`/`'grupal'` correctamente, esa hipótesis inicial del spec era incorrecta. Causa real encontrada (ver `research.md` R1): `usp_Conversaciones_Crear` es un `PROCEDURE` invocado vía `CALL`; `ConversacionHandler.CrearAsync` mandaba `p_asunto`/`p_licitacion_id` sin `dbType` cuando son `null` (viajan como parámetro `unknown`) y `p_participante_ids` sin cast a `jsonb` — Postgres no resolvía ninguna sobrecarga (`42883`). Fix aplicado: `dbType` explícito para `p_tipo`/`p_asunto`/`p_licitacion_id`/`p_creador_id` en `src/MPM.Modules.Mensajeria/Data/ConversacionHandler.cs`, y `@p_participante_ids::jsonb` en `src/MPM.Modules.Mensajeria/Data/MensajeriaStoredProcedures.cs`.
- [X] T003 [US1] Verificado en local (`docker compose`, contenedor reconstruido): `POST /api/v1/conversaciones` con `{"tipo":"directo","asunto":null,"licitacionId":null,"participanteIds":["2"]}` → 200 OK (antes: 400/`42883`). Repetido con `{"tipo":"grupal","asunto":"Grupo de prueba",...,"participanteIds":["2","3"]}` → 200 OK.
- [X] T004 [P] [US1] Agregado `tests/MPM.Modules.Mensajeria.Tests/Data/ConversacionHandlerCrearAsyncTests.cs` (source-guard, 2 tests) — verifica el cast `::jsonb` y el `dbType` explícito. **Nota**: `MPM.Modules.Mensajeria.Tests` no está registrado en `MPM.sln` (gap preexistente, ya documentado en `022-qa-fixes-preproduccion`) — se corrió directo con `dotnet test tests/MPM.Modules.Mensajeria.Tests`, 2/2 verde.

**Checkpoint**: US1 completo — crear conversaciones nuevas (directas y grupales) funciona de punta a punta en local, causa raíz real corregida y con test de regresión.

---

## Phase 4: User Story 2 — Telegram habilitado tras auto-vinculación (P1) — BUG-015

**Goal**: Que cualquier usuario que se auto-vincule a Telegram (deep-link o Chat ID manual) quede realmente habilitado para recibir alertas, y que los usuarios ya vinculados antes del fix queden habilitados retroactivamente.

**Independent Test**: Vincular Telegram con un usuario nuevo, crear una alerta con `notificarTelegram=true`, usar "Probar alerta", confirmar `notificacionTelegramEnviada: true` y que el mensaje llega al chat real.

- [X] T005 [US2] Creado `src/MPM.Api/Database/Scripts/V096__Fix_Alertas_Destinatarios_Telegram.sql` con `CREATE OR REPLACE FUNCTION usp_AlertasDestinatarios_GuardarChatId(...)` seteando `es_account_manager_gobierno = TRUE` en `INSERT` y `ON CONFLICT DO UPDATE`.
- [X] T006 [US2] Backfill incluido en el mismo `V096`: `UPDATE alertas_destinatarios SET es_account_manager_gobierno = TRUE ... WHERE telegram_chat_id IS NOT NULL AND es_account_manager_gobierno = FALSE;`.
- [X] T007 [P] [US2] Agregado `tests/MPM.Modules.Alertas.Tests/Data/AlertasDestinatariosTelegramFixTests.cs` (source-guard sobre el SQL de V096, 2 tests) — 2/2 verde, corrido junto al resto de `MPM.Modules.Alertas.Tests` (27/27 verde, sin regresiones).
- [X] T008 [US2] Migración V096 aplicada en local (confirmado vía `_migrations`). Validado con `curl` directo: `POST /api/v1/alertas/mi-telegram` con un chat_id de prueba → `SELECT es_account_manager_gobierno FROM alertas_destinatarios` = `t` (antes del fix hubiera quedado en `f`, el DEFAULT).
- [X] T009 [US2] Backfill confirmado: fila de prueba insertada manualmente con `telegram_chat_id` no nulo y `es_account_manager_gobierno = FALSE`, se corrió el `UPDATE` del backfill, quedó en `TRUE`. Fila de prueba eliminada después.

**Checkpoint**: US2 completo — la auto-vinculación de Telegram habilita la entrega real, con y sin backfill, verificado en local.

---

## Phase 5: Polish & Deploy

- [X] T010 `dotnet build MPM.sln` — compilación correcta, 0 errores. `dotnet test MPM.sln` — sin regresiones en ningún proyecto del solution.
- [X] T011 `mpm-api` desplegado a producción (`tivit-cu010`, revisión `mpm-api-00006-f2k`) con ambos fixes. Migración V096 confirmada aplicada vía logs (`Migration V096 applied successfully.`).
- [X] T012 ~~Desplegar frontend~~ — **No aplica**: el fix real de BUG-014 fue 100% backend (tipado de parámetros Dapper/Npgsql), el frontend ya estaba correcto. No hizo falta redeploy de `mpm-web`.
- [X] T013 Verificado en vivo contra producción real: `POST /api/v1/conversaciones` (tipo directo) → 201 (antes 400). `POST /api/v1/alertas/mi-telegram` → 200, y `POST /api/v1/alertas/1/probar` sobre una licitación real (`622-12-LP26`) → `notificacionTelegramEnviada: true` (antes: `false` sin error visible) — confirmado sin usar el bypass manual de la demo, el sistema real ahora manda la alerta.
- [X] T014 `CLAUDE.md` actualizado: `023-fix-bugs-produccion` marcado resuelto, migración más alta aplicada = **V096**, puntero de plan vuelto a la prioridad vigente.

## Dependencies

- **US1 (Fase 3)** y **US2 (Fase 4)** son completamente independientes — se pueden trabajar en paralelo o en cualquier orden.
- **Fase 5 (Polish & Deploy)** depende de que ambas historias estén completas y validadas en local.

## Parallel Execution Examples

```text
# US1 y US2 en paralelo (agentes/desarrolladores distintos):
T002 (frontend, mensajería) ‖ T005 (SQL, alertas)
T004 (test mensajería)      ‖ T007 (test alertas)
```

## Implementation Strategy

**MVP = ambas historias**, dado que las dos son P1 y bloquean funcionalidad ya mostrada al cliente en la demo del 2026-07-09. No hay una historia "más mínima" que la otra — se recomienda resolver ambas antes del siguiente contacto con el cliente, en paralelo si hay más de una persona disponible.
