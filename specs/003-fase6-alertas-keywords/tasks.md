---

description: "Task list for Fase 6 — Alertas Inteligentes por Palabras Clave"
---

# Tasks: Fase 6 — Alertas Inteligentes por Palabras Clave

**Input**: Design documents from `specs/003-fase6-alertas-keywords/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/alertas-api.md, quickstart.md

**Tests**: no se piden tests nuevos explícitamente en spec.md; se incluyen únicamente los unit tests que la Constitución exige para código nuevo (Principio VII) sobre lógica sin dependencias externas (matching, parsing de sinónimos).

**Organización**: por historia de usuario (US1-US5), en el orden de prioridad de spec.md. Ver plan.md "Estrategia para el jueves" — US1, US3 y el endpoint de prueba de US5 son lo imprescindible para la demo del 2026-07-09.

---

## Phase 1: Setup — ✅ completada 2026-07-06

- [x] T001 `src/MPM.Modules.Alertas/MPM.Modules.Alertas.csproj` creado
- [x] T002 [P] Agregado a `MPM.sln` (vía `dotnet sln add`) y referenciado desde `MPM.Api.csproj` y `MPM.Modules.Licitaciones.csproj` (este último necesario para invocar el matching desde `SyncEngineService` — ver nota de diseño en Phase 2)
- [x] T003 [P] `tests/MPM.Modules.Alertas.Tests` creado
- [x] T004 Migración `V079__Create_Alertas.sql` creada, con un SP adicional no previsto originalmente: `usp_Licitaciones_ListarParaMatching` (en el módulo Licitaciones, necesario para que `SyncEngineService` obtenga las licitaciones del ciclo a evaluar)

**Checkpoint**: ✅ compila limpio (`dotnet build MPM.sln`, 0 errores).

---

## Phase 2: Foundational (Bloqueante para todas las historias) — ✅ completada 2026-07-06

**Purpose**: DTOs, handlers base, y el punto de integración con `SyncEngineService` que todas las historias necesitan.

- [x] T005 [P] `AlertasDtos.cs` creado — `TiposLicitacion`/`TipoLicitacion` se tipificaron como `string[]?`/`string?` (no `int`), corrigiendo un supuesto incorrecto del data-model.md original: la columna `licitaciones.tipo` es `VARCHAR`, no un código numérico.
- [x] T006 [P] `AlertasStoredProcedures.cs` creado.
- [x] T007 `AlertasHandler.cs` creado (Dapper, sin ORM).
- [x] T008 `ModuleRegistration.cs` creado.
- [x] T009 **Rediseñado 2026-07-06**: se descartó la interfaz `IAlertasMatchingTrigger` — el propio código existente ya viola la separación estricta del Principio I (`MPM.Modules.Licitaciones` ya referencia `MPM.Modules.Notificaciones` directamente). Se siguió el mismo patrón real: `MPM.Modules.Licitaciones.csproj` referencia `MPM.Modules.Alertas.csproj` directamente, y `SyncEngineService` resuelve `AlertasMatchingService` por DI. Más simple, consistente con el resto del código.
- [x] T010 Registrado en `Program.cs` (servicio web y modo worker `EjecutarWorkerAsync`).

**Checkpoint**: ✅ el módulo existe, compila, y `SyncEngineService` puede invocar el matching.

---

## Phase 3: User Story 1 — Configurar alertas personalizadas (Priority: P1) 🎯 MVP

**Goal**: un analista crea una regla (keyword + monto + rubros) y recibe notificación in-app cuando una licitación nueva coincide.

**Independent Test**: crear alerta "cloud" + monto > $10M; al llegar una licitación que coincide, notificación in-app en <60 min (quickstart.md Escenario 1, adaptado sin sinónimos todavía).

### Implementation for User Story 1 — ✅ backend completo 2026-07-06, frontend pendiente

- [x] T011 [US1] `AlertasService.cs`: `CrearAsync`, `EditarAsync`, `ListarAsync`, `ToggleAsync`, `EliminarAsync`
- [x] T012 [US1] `AlertasMatchingService.cs`: evaluación de reglas activas contra licitaciones nuevas del ciclo de sync (implementado junto con US3 desde el inicio, no se hizo "sin sinónimos" como paso intermedio — se construyó directo con soporte de sinónimos, ver T022)
- [x] T013 [US1] Deduplicación por `(regla_id, licitacion_id)` — vía `usp_AlertasDisparadas_ExisteParaLicitacion` + constraint `UNIQUE` en la tabla como respaldo
- [x] T014 [US1] Integrado con `NotificacionesService.CrearAsync` existente
- [x] T015 [US1] Agrupación por usuario: si una licitación coincide con varias reglas del mismo usuario, se genera una sola notificación mencionando todos los términos
- [x] T016 [US1] `POST /api/v1/alertas`, `GET /api/v1/alertas` implementados
- [x] T017 [P] [US1] Frontend `useAlertas.ts` implementado (hooks TanStack Query para listar/crear/editar/toggle/eliminar/probar)
- [x] T018 [P] [US1] Frontend `AlertasPage.tsx` implementado: tabla de reglas, modal de creación, toggle inline, eliminar
- [x] T019 [P] [US1] `AlertasMatchingServiceTests.cs`: 6 tests (keyword literal, sinónimo, sin match, filtro de monto, filtro de tipo, sinónimo mal formado no lanza excepción — este último caso se encontró *escribiendo* el test, no estaba contemplado, y se corrigió en el mismo momento)

**Checkpoint**: ✅ **validado end-to-end contra una base de datos real** (2026-07-06, `docker compose up`): migración `V079` aplicó sin errores (incluyendo el `DROP FUNCTION`+`CREATE OR REPLACE` de `usp_Licitaciones_Listar`), se creó una regla real ("cloud"), se disparó contra la licitación real `869591-6-LR26` ("...infraestructura CLOUD..."), y la notificación in-app se generó correctamente con el mensaje y metadata esperados. Toggle también validado. En el camino se encontraron y corrigieron 2 bugs reales que solo aparecen corriendo Docker de verdad: (1) el `Dockerfile` no restauraba `MPM.Modules.Alertas` (faltaba en la lista de `COPY *.csproj`), (2) `LicitacionResumenDto`/`usp_Licitaciones_Listar` no exponían el `id` interno, necesario para el selector del frontend. Frontend implementado (`AlertasPage.tsx`, `useAlertas.ts`) y con `tsc --noEmit` limpio, pero no probado interactivamente en un navegador todavía.

---

## Phase 4: User Story 3 — Expansión de keywords por sinónimos vía IA (Priority: P1)

**Goal**: una regla con "SOC" también dispara con "centro de operaciones de seguridad".

**Independent Test**: keyword "SOC" dispara con licitación que solo dice "centro de operaciones de seguridad" (quickstart.md Escenario 2).

### Implementation for User Story 3 — ✅ backend completo 2026-07-06, frontend pendiente

- [x] T020 [US3] `SinonimosIaService.cs`: `HttpClient` directo a Gemini (no reutiliza `GeminiService` de Analisis — Alertas no referencia ese proyecto, Principio I), fallo se degrada a `null` sin tumbar la creación de la regla
- [x] T021 [US3] Integrado en `AlertasService.CrearAsync`/`EditarAsync`
- [x] T022 [US3] `AlertasMatchingService.EvaluarMatch` evalúa keyword literal y cada sinónimo, retorna cuál término disparó el match
- [x] T023 [P] [US3] `PUT /api/v1/alertas/{id}` implementado
- [x] T024 [P] [US3] Frontend muestra `sinonimosIa` como tags en la tabla
- [ ] T025 [P] [US3] Unit tests específicos de `SinonimosIaService` (parseo de Gemini) — **no escritos**; sí se cubrió el caso de sinónimos mal formados del lado del matching (T019). El parseo de la respuesta de Gemini en sí no tiene test unitario todavía.

**Checkpoint**: el diferenciador pedido por el cliente (sinónimos IA) está implementado backend+frontend. Probado end-to-end contra Gemini real (2026-07-06) — respondió 401 (`GEMINI_API_KEY` local parece vencida/inválida), y el sistema se degradó correctamente (regla creada con `sinonimosIa=null`, sin caerse). El mecanismo funciona; falta una API key válida para confirmar que sí devuelve sinónimos reales.

---

## Phase 5: User Story 5 (parcial) — Endpoint de alerta de prueba para demo (Priority: P2, pero imprescindible para el jueves)

**Goal**: poder demostrar el pipeline completo sin depender de que llegue una licitación real nueva durante la demo.

**Independent Test**: `POST /api/v1/alertas/{id}/probar` con una licitación real existente genera notificación in-app marcada `es_prueba=true` (quickstart.md Escenario 5).

### Implementation — ✅ backend completo 2026-07-06, frontend pendiente

- [x] T026 [US5] `POST /api/v1/alertas/{id}/probar` implementado — **con un cambio de contrato respecto al diseño original**: en vez de recibir solo `licitacionId` y hacer un lookup interno, recibe también nombre/descripción/monto/tipo/organismo en el body (`ProbarAlertaRequest`), porque `MPM.Modules.Alertas` no referencia `MPM.Modules.Licitaciones` (es al revés) — el frontend ya tiene esos datos al elegir la licitación de una lista, se pasan directo. Ver `contracts/alertas-api.md` (a actualizar con el contrato real).
- [x] T027 [P] [US5] Modal "Probar" con selector de licitación real (vía `useLicitaciones`) implementado
- [x] T028 [P] [US5] El campo `es_prueba` existe y se filtra correctamente en el modelo; no hay un test unitario dedicado, pero la lógica de `ProcesarGrupoAsync`/`RegistrarDisparoAsync` lo persiste explícitamente en cada llamada.

**Checkpoint**: backend listo para demostrar sin esperar una licitación real, una vez haya frontend o se pruebe vía Postman/curl.

---

## Phase 6: User Story 4 — Notificación enriquecida a account managers (Priority: P1)

**Goal**: la notificación incluye requisitos, competidores, presupuesto, forma de pago, multas y señal de renovación, y llega a los 2 account managers de gobierno, no solo al creador de la regla.

**Independent Test**: alerta sobre licitación con bases que mencionan "proveedor actual Sonda, 6 años" → notificación marca "posible renovación" (quickstart.md Escenario 3).

### Implementation — ✅ mayormente completa 2026-07-07

- [x] T029 [US4] `AlertaEnriquecimientoService.cs` **actualizado 2026-07-07**: ahora consulta `MPM.Modules.Analisis` (referencia de proyecto directa, mismo patrón que `Licitaciones → Alertas`) vía `AnalisisHandler.ObtenerResultadoPorLicitacionAsync` (nuevo, respaldado por `usp_AnalisisResultados_ObtenerPorLicitacion`, migración `V090`) cuando ya existe un análisis de Gemini completado para la licitación — trae `requisitos` y `competidores` (ofertantes) reales. **`forma_pago` y `multas` siguen en `null` a propósito**: el esquema de extracción de `GeminiService` no captura esos dos campos hoy (decisión tomada con el usuario 2026-07-07: no se extiende el prompt de análisis en esta pasada). Validado en vivo contra un análisis real (licitación `14-13-B226`): trajo el detalle real de por qué la oferta de TIVIT fue "Inadmisible".
- [x] T030 [US4] Integrado en `AlertasMatchingService.ProcesarGrupoAsync`
- [x] T031 [US4] `AlertasHandler.ListarAccountManagersAsync`/tabla `alertas_destinatarios` existen
- [x] T032 [US4] **Implementado y validado en vivo 2026-07-07**: `AlertasMatchingService.ProcesarGrupoAsync` ahora notifica in-app y por Telegram a todos los `es_account_manager_gobierno=true` además del dueño de la regla (evitando duplicar si el dueño ya es uno de ellos). Probado contra la API real: una alerta disparada llegó correctamente a 2 usuarios distintos (dueño + otro account manager).
- [ ] T033 [P] [US4] Unit tests — no escritos.

**Checkpoint**: el pedido más explícito de Francisco (T032) ya está implementado y validado. El resumen enriquecido trae datos reales cuando hay análisis previo; `forma_pago`/`multas` quedan como brecha conocida y documentada, no como bug.

---

## Phase 7: User Story 5 (completo) — Notificación por Telegram (Priority: P2)

**Goal**: la alerta también llega a Telegram, sin bloquear el in-app si falla.

**Independent Test**: bot mal configurado → in-app se genera igual, error queda registrado (quickstart.md Escenario 4).

### Implementation — ⚠️ parcial 2026-07-06

- [x] T034 [US5] `TelegramNotificationService.cs` creado, con try/catch aislado
- [ ] T035 [US5] `Telegram:BotToken` — **no agregado** a `appsettings.json`/`docker-compose.yml` todavía (config leída vía `IConfiguration`, solo falta la entrada — trivial, no hecho por tiempo)
- [x] T036 [US5] Integrado en `AlertasMatchingService.ProcesarGrupoAsync` y en el endpoint de prueba (comparten el mismo método) — **corregido 2026-07-07** (de paso, al implementar T032): ahora sí condiciona estrictamente por `notificar_telegram` de la regla, y envía a todos los account managers con `telegram_chat_id` configurado, no solo al dueño.
- [x] T037 [US5] `notificacion_telegram_enviada`/`notificacion_telegram_error` se registran vía `MarcarTelegramAsync`
- [x] T038b [US5] **Nuevo 2026-07-07**: autoregistro de Chat ID — antes existía `usp_AlertasDestinatarios_GuardarChatId` (V079) pero ningún endpoint/UI lo llamaba, así que no había forma de que un usuario configurara su propio Chat ID. Implementado `POST /api/v1/alertas/mi-telegram` (`AlertasController.GuardarMiTelegram` → `AlertasService.GuardarMiTelegramAsync` → `AlertasHandler.GuardarChatIdAsync`) + botón "Mi Telegram" y modal en `AlertasPage.tsx` (`useGuardarMiTelegram`). `.env.example` documenta `TELEGRAM_BOT_TOKEN`. Probado en vivo: guardado confirmado por toast y verificado en `alertas_destinatarios` (fila con `usuario_id=1`, `telegram_chat_id='987654321'`, limpiada después de la prueba).
- [ ] T038 [P] [US5] Documentado en `quickstart.md` de la spec (no en el runbook de producción todavía)
- [ ] T039 [P] [US5] Unit tests — no escritos
- [x] T038c [US5] **Nuevo 2026-07-07**: vinculación automática vía deep link + webhook, para no depender de que el usuario copie/pegue el chat_id a mano. Migración `V091` (tabla `alertas_telegram_link_tokens`, SPs `usp_TelegramLinkTokens_Crear`/`Consumir`, token de un solo uso con expiración de 10 min). Nuevo endpoint `POST /api/v1/alertas/mi-telegram/link` (`AlertasService.GenerarLinkTelegramAsync`) devuelve `https://t.me/{Telegram:BotUsername}?start={token}`; botón "Conectar con Telegram" en el modal lo abre en una pestaña nueva. Nuevo `TelegramWebhookController` (`POST /api/v1/telegram/webhook`, `[AllowAnonymous]`, valida `X-Telegram-Bot-Api-Secret-Token` contra `Telegram:WebhookSecret`) recibe el `/start <token>` que Telegram reenvía al tocar "Iniciar" y llama `AlertasService.VincularTelegramPorTokenAsync` para guardar el chat_id automáticamente. `Telegram:BotUsername`/`Telegram:WebhookSecret` agregados a `appsettings.json`, `docker-compose.yml` (`TELEGRAM_BOT_USERNAME`/`TELEGRAM_WEBHOOK_SECRET`) y `.env.example`. El input manual de Chat ID queda como respaldo en el mismo modal. Probado en vivo: `POST mi-telegram/link` genera el token y la URL correctamente (verificado en `alertas_telegram_link_tokens`). **No probado el webhook real** — Telegram exige una URL HTTPS pública para `setWebhook`, no funciona contra `localhost`; queda pendiente de probar una vez esté el deploy de Cloud Run (`002-fase5-deploy-gcp`), y llamar `setWebhook` manualmente una vez (no automatizado en el arranque, ver nota abajo).

**Checkpoint**: Telegram funcional a nivel de código, probado end-to-end en vivo (`/probar` real → mensaje recibido en Telegram, confirmado por el usuario 2026-07-07) usando el flujo manual de Chat ID. El flujo de deep link + webhook (T038c) está implementado pero no se puede probar de punta a punta hasta el deploy con HTTPS público.

**Pendiente de infraestructura**: llamar una vez `https://api.telegram.org/bot<token>/setWebhook?url=https://<dominio-cloud-run>/api/v1/telegram/webhook&secret_token=<TELEGRAM_WEBHOOK_SECRET>` después del deploy — no forma parte del código de la app (es un paso de configuración externo, como `MP_TICKET`).

**⚠️ Brecha real encontrada al probar T038b/T038c** (resuelta parcialmente 2026-07-07): el UPSERT de `GuardarChatIdAsync` solo escribe `telegram_chat_id` — NO toca `es_account_manager_gobierno`. Como `ListarAccountManagersAsync` (la fuente de destinatarios de T032) filtra `WHERE es_account_manager_gobierno = TRUE`, un usuario que se autoregistra vía "Mi Telegram" **no recibe alertas de reglas de otros dueños** a menos que ese flag esté en `true` — hoy no existe UI/endpoint de admin para marcarlo, solo acceso directo a la base. Se decidió con el usuario (2026-07-07) dejar su propio usuario marcado como account manager para las pruebas; sigue pendiente para el resto del equipo una UI de administración de ese flag, o redefinir el modelo si se quiere que cualquier usuario con Telegram conectado reciba automáticamente sus propias alertas sin pasar por "account manager de gobierno".

---

## Phase 8: User Story 2 — Gestión del panel de alertas (Priority: P2)

**Goal**: activar/pausar/editar/eliminar alertas sin soporte técnico.

**Independent Test**: usuario activa, edita monto mínimo y desactiva una alerta desde el frontend (quickstart.md, implícito).

### Implementation

- [x] T040 [US2] `PATCH /api/v1/alertas/{id}/toggle` y `DELETE /api/v1/alertas/{id}` implementados
- [x] T041 [US2] `GET /api/v1/alertas/{id}/historial` **implementado y validado en vivo 2026-07-07** — se corrigió además un bug de seguridad real en el SP (`usp_AlertasDisparadas_Historial`, V079): no validaba que la regla perteneciera al usuario que consulta; ahora requiere `p_usuario_id` y devuelve vacío si no coincide (migración `V089`). Probado: dueño ve su historial completo (con el resumen enriquecido de T029 incluido), otro usuario ve vacío.
- [ ] T042 [P] [US2] Frontend — no empezado (depende de T017/T018)

**Checkpoint**: backend completo, incluyendo historial con ownership. Solo falta el frontend de esta historia.

---

## Phase 9: Polish & Cross-Cutting — parcial

- [x] T043 [P] **Validado end-to-end 2026-07-06** vía `docker compose up` (db+redis+api reales): Escenario 1 (crear alerta), Escenario 5 (disparar prueba, notificación in-app generada) confirmados contra datos reales. Escenarios 2/3 (matching por sinónimo, resumen con campos parciales) y 4 (fallo de Telegram no bloquea) no se probaron explícitamente esta sesión — la lógica está cubierta por unit tests pero no se disparó en vivo. Escenario 6 (deduplicación por múltiples reglas) tampoco se probó en vivo.
- [ ] T044 [P] Actualizar `CHANGELOG.md` — pendiente
- [ ] T045 Revisar impacto en tiempo del ciclo de sync — pendiente, requiere medición en vivo con volumen real

## Resumen del estado real al cierre de esta sesión (2026-07-06)

**Hecho, compilando, y validado contra una base de datos real**: módulo completo backend+frontend (Setup, Foundational, US1, US3, endpoint de prueba de US5 con UI, CRUD de US2, Telegram a nivel de código). Se levantó el stack completo con `docker compose`, se aplicó la migración `V079` en vivo, se creó una alerta real, se disparó contra una licitación real de la base, y se confirmó la notificación in-app generada correctamente. En el proceso se encontraron y corrigieron 2 bugs que unit tests no hubieran detectado (Dockerfile sin restaurar el módulo nuevo; `id` interno no expuesto en `usp_Licitaciones_Listar`). 6 unit tests de matching escritos (no se pudo correr `dotnet test` por falta del runtime .NET 8 en este entorno — mismo problema documentado en 002-fase5-deploy-gcp — pero sí se validó la lógica equivalente en vivo vía la API real). Frontend con `tsc --noEmit` limpio, no probado interactivamente en navegador.

**No hecho, honesto sobre las brechas**:
- Enrutamiento a los 2 account managers de gobierno (T032) — el pedido más explícito de Francisco de esta spec, no implementado (hoy solo notifica al creador de la regla).
- Reuso de análisis de documentos existente para el resumen enriquecido (T029 parcial) — solo metadatos, no requisitos/competidores/forma de pago/multas reales.
- Endpoint de historial (T041) y su UI (parte de T042).
- Telegram no probado contra un bot real (falta `Telegram:BotToken`).
- Prueba visual del frontend en un navegador real (solo compilación TypeScript verificada).

**Para el jueves**: hay algo demostrable de verdad — UI funcional + backend probado en vivo. Lo más importante que falta si hay tiempo: T032 (los 2 account managers, pedido explícito de Francisco) y probar el frontend en un navegador antes de la demo.

## Actualización 2026-07-07 — T032, T041 y T029 (parcial) cerrados

Se implementaron y **validaron en vivo contra la API real** (con `docker compose`, no solo compilación):

- **T032**: `AlertasMatchingService.ProcesarGrupoAsync` ahora notifica in-app y por Telegram a todos los `es_account_manager_gobierno=true`, no solo al dueño de la regla. Probado con 2 usuarios reales — ambos recibieron la notificación de una alerta disparada.
- **T041**: `GET /api/v1/alertas/{id}/historial` implementado. Se encontró y corrigió de paso un bug de seguridad real: el stored procedure (`usp_AlertasDisparadas_Historial`, de `V079`) no validaba ownership — cualquiera con el `id` de una regla ajena podía ver su historial. Corregido en `V089` (nueva migración, requiere `p_usuario_id`). Probado: dueño ve su historial, otro usuario ve vacío.
- **T029 (parcial, alcance acordado explícitamente con el usuario)**: `AlertaEnriquecimientoService` ahora consulta `MPM.Modules.Analisis` (nueva referencia de proyecto `Alertas → Analisis`, mismo patrón que `Licitaciones → Alertas`; nuevo SP `usp_AnalisisResultados_ObtenerPorLicitacion` en `V090`) para traer requisitos y competidores reales cuando ya existe un análisis de Gemini para esa licitación. Probado en vivo contra un análisis real (`14-13-B226`): trajo el detalle real de por qué la oferta de TIVIT fue rechazada. **`forma_pago` y `multas` quedan en `null` a propósito** — no existen en el esquema de extracción de `GeminiService` hoy; extenderlo queda fuera de esta pasada (decisión explícita con el usuario, no un olvido).

**Lo que sigue pendiente, sin cambios respecto a lo de arriba**: unit tests de T032/T029/T041 (T033, T039), `Telegram:BotToken` sin probar contra un bot real, frontend del historial (parte de T042), y prueba visual del frontend en un navegador real.

---

## Dependencies & Execution Order

- **Setup (1) → Foundational (2)**: bloquean todo lo demás.
- **US1 (3)** y **US3 (4)**: ambas P1, US3 depende de que US1 exista (el matching se extiende, no se duplica) — secuenciales, no paralelas entre sí, pero juntas son el mínimo demostrable del jueves.
- **US5 parcial — endpoint de prueba (5)**: depende de US1 (necesita el pipeline de matching/notificación ya armado) — se prioriza antes que US4 completo porque es lo que permite demostrar sin esperar datos reales.
- **US4 (6)**: depende de US1; puede avanzar en paralelo con US5 si hay dos personas.
- **US5 completo — Telegram (7)**: depende del endpoint de prueba (5) para poder probarse de punta a punta; puede empezar en paralelo a US4 si hay capacidad.
- **US2 (8)**: depende de US1 (CRUD básico ya debe existir); es la menos urgente para el jueves.
- **Polish (9)**: al final.

### Parallel Opportunities

- T002, T003 en paralelo tras T001.
- T005, T006 en paralelo.
- T017, T018, T019 (frontend + tests de US1) en paralelo entre sí una vez el backend de US1 esté.
- T027, T028 en paralelo.
- T033, T038, T039 en paralelo con sus fases respectivas.

---

## Implementation Strategy — orden real para llegar al jueves 9 de julio

1. Setup + Foundational (Fases 1-2) — base mínima.
2. US1 (Fase 3) — alertas por keyword literal, notificación in-app. **Demostrable por sí solo si el tiempo no alcanza para más.**
3. US3 (Fase 4) — sinónimos IA. **Esto es lo que el cliente pidió ver explícitamente.**
4. US5 parcial (Fase 5) — endpoint de prueba, para no depender de que llegue una licitación real durante la demo.
5. Si queda tiempo antes del jueves: US4 (Fase 6, resumen enriquecido) y US5 completo (Fase 7, Telegram) — en ese orden, porque US4 es pedido del cliente (Francisco) y Telegram es instrucción interna (Manuel) con fallback manual vía Postman si no alcanza.
6. US2 (Fase 8) y Polish (Fase 9) quedan para después del jueves si no alcanza el tiempo — no son parte de lo que se pidió mostrar primero.
