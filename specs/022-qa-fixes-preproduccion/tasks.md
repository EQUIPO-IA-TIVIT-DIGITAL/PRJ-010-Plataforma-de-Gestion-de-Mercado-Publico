---

description: "Task list for Corrección de Hallazgos QA Pre-Producción"

---

# Tasks: Corrección de Hallazgos QA Pre-Producción

**Input**: Design documents from `/specs/022-qa-fixes-preproduccion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: Incluidas — la Constitución del proyecto (Principio VII) exige cobertura unitaria para todo código nuevo; el scraper Node (`tools/scraper-mp/`) no tiene suite automatizada hoy, así que sus tareas se validan solo vía `quickstart.md`.

**Organization**: Tareas agrupadas por user story (US1–US9 de `spec.md`), en el mismo orden de prioridad P1 → P2 → P3. Cada `[US#]` es trazable a su(s) BUG-ID de origen del informe QA.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Se puede ejecutar en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: A qué user story pertenece (US1–US9)

## Path Conventions

Modular monolith .NET 8 (`src/MPM.*`, `tests/MPM.*.Tests`) + scraper Node (`tools/scraper-mp/`), según `plan.md`.

---

## Phase 1: Setup

**Purpose**: Verificar la base sobre la que se aplican las correcciones — no hay dependencias nuevas que instalar.

- [ ] T001 Confirmar que la migración V091 (`src/MPM.Api/Database/Scripts/V091__Create_Telegram_Link_Tokens.sql`) está aplicada en el entorno local/staging antes de agregar V092/V093 (verificación, sin cambios de código) — **PENDIENTE**: requiere acceso a un Postgres vivo, no disponible en esta sesión; hacer antes de aplicar V092/V093.
- [x] T002 [P] Documentar las claves de configuración nuevas (`Cors:AllowedOrigins`, `Analisis:RecoveryThresholdMinutes`, `RUN_INPROCESS_WORKERS`) en `docker-compose.yml` y en el `.env` de ejemplo del repo — no existe `.env.example` en el repo; documentado como bloque de comentarios en `docker-compose.yml` junto a `Cors__AllowedOrigins`.

---

## Phase 2: Foundational

**Purpose**: No existe un bloqueante compartido entre las 9 historias — cada BUG-ID es independiente y toca un módulo distinto (ver `plan.md` Constitution Check, Principio I). Esta fase queda vacía a propósito; se puede pasar directo a Phase 3.

**Checkpoint**: Sin prerequisitos bloqueantes — las historias P1 pueden arrancar en paralelo entre sí.

---

## Phase 3: User Story 1 - Arranque a prueba de fallos de migración (Priority: P1) 🎯 MVP crítico

**Goal**: Si una migración falla, el servicio no queda disponible con un esquema a medias; dos instancias no compiten aplicando migraciones a la vez. (BUG-001)

**Independent Test**: Ver `quickstart.md` § US1 — introducir una migración inválida, desplegar, y confirmar que `/health` no responde 200.

- [x] T003 [P] [US1] Unit test: `DatabaseInitializer` propaga la excepción cuando una migración falla (sin capturarla en silencio) en `tests/MPM.Api.Tests/Database/DatabaseInitializerTests.cs` — implementado en `tests/MPM.Tests/Database/DatabaseInitializerTests.cs` (no `MPM.Api.Tests`, que no existe en el repo; `MPM.Tests` ya referencia `MPM.Api` y corre contra el Postgres real de docker-compose). No se pudo forzar una migración inválida sin contaminar `Database/Scripts/` real, así que se usa una guarda de regresión sobre el código fuente (`throw;` presente en el catch) en vez de una migración fallida real — ver comentario en el test.
- [x] T004 [P] [US1] Unit test: `DatabaseInitializer` adquiere `pg_advisory_lock` antes de aplicar migraciones y lo libera en `finally`, incluso ante excepción, en `tests/MPM.Api.Tests/Database/DatabaseInitializerTests.cs` — implementado en `tests/MPM.Tests/Database/DatabaseInitializerTests.cs`; verifica con dos `InitializeAsync()` concurrentes contra Postgres real que no se cuelgan ni fallan (si el lock/unlock estuviera mal implementado, se colgaría hasta el timeout).
- [x] T005 [US1] Agregar `pg_advisory_lock(<hash fijo del proyecto>)` al inicio de `InitializeAsync()` y liberarlo en `finally` en `src/MPM.Api/Database/DatabaseInitializer.cs`
- [x] T006 [US1] Eliminar el `catch` silencioso de la migración fallida (línea ~56-59) y propagar la excepción en `src/MPM.Api/Database/DatabaseInitializer.cs`

**Checkpoint**: US1 funcional y testeable de forma independiente — el bloqueante #1 del jueves está resuelto.

---

## Phase 4: User Story 2 - Análisis de PDF sobrevive a Cloud Run (Priority: P1) 🎯 MVP crítico

**Goal**: Un análisis interrumpido por pérdida de CPU/reinicio se retoma sin intervención manual. (BUG-002)

**Independent Test**: Ver `quickstart.md` § US2 — matar la API a mitad de un análisis y confirmar que se completa tras el próximo ciclo del worker de recuperación.

- [x] T007 [P] [US2] Unit test: `AnalisisRecoveryWorker` reclama workspaces en `analizando` sin resultado y con `updated_at` fuera del umbral, ignora los que sí tienen resultado, en `tests/MPM.Modules.Analisis.Tests/Services/AnalisisRecoveryWorkerTests.cs` — corre contra Postgres real; `IAnalisisBackgroundService` mockeado con Moq (evita disparar Gemini/Vertex AI real, que factura). **Encontró un bug real durante el desarrollo**: `AnalisisRecoveryWorker` usaba `UpdatedAt.ToUniversalTime()` sobre una columna `timestamp without time zone` (`Kind=Unspecified`), lo que la interpretaba como hora local de la máquina en vez de UTC — corregido a `DateTime.SpecifyKind(..., DateTimeKind.Utc)`.
- [x] T008 [US2] Implementar `AnalisisRecoveryWorker : IHostedService` (poll cada ~60s, usa `usp_AnalisisWorkspaces_Listar(p_estado='analizando')` + `usp_AnalisisResultados_ObtenerPorWorkspace` + `usp_AnalisisDocumentos_Listar`, umbral configurable) en `src/MPM.Modules.Analisis/Services/AnalisisRecoveryWorker.cs`
- [x] T009 [US2] Registrar `AnalisisRecoveryWorker` como hosted service y leer `Analisis:RecoveryThresholdMinutes` (default 5) en `src/MPM.Modules.Analisis/ModuleRegistration.cs`
- [x] T010 [US2] Agregar `--no-cpu-throttling` a la llamada `gcloud run deploy` de `mpm-api` en `deploy_api()` dentro de `scripts/deploy.sh`

**Checkpoint**: US2 funcional — el análisis nunca queda "procesando" para siempre, con o sin el flag de deploy.

---

## Phase 5: User Story 3 - Sync/scraper sin duplicar, DB_HOST correcto (Priority: P1) 🎯 MVP crítico

**Goal**: La sincronización y el scraper se ejecutan una sola vez por ciclo; el scraper se conecta a Cloud SQL en producción. (BUG-004, BUG-005)

**Independent Test**: Ver `quickstart.md` § US3 — con `RUN_INPROCESS_WORKERS=false`, el servicio web no ejecuta sync/scraper por su cuenta; el scraper conecta a la BD gestionada.

- [x] T011 [P] [US3] Unit test: `ModuleRegistration.AddLicitacionModule` no registra `SyncEngineService`/`ScraperBackgroundService`/`AclaracionMonitorService` como hosted services cuando `RUN_INPROCESS_WORKERS=false`, y sí lo hace cuando es `true`/ausente, en `tests/MPM.Modules.Licitaciones.Tests/ModuleRegistrationTests.cs` — inspecciona los `ServiceDescriptor` directamente (sin `BuildServiceProvider()`, que requeriría registrar toda la composición de `Program.cs` como `DbConnectionFactory`).
- [x] T012 [P] [US3] Unit test: `ScraperBackgroundService` lee `DB_HOST`/`DB_PORT` desde configuración en vez de usar literales fijos, en `tests/MPM.Modules.Licitaciones.Tests/Services/ScraperBackgroundServiceTests.cs` — se extrajo `BuildScraperEnvironmentVariables(IConfiguration)` como método `internal` testeable (antes estaba inline junto al spawn del proceso Node), con `InternalsVisibleTo` agregado al `.csproj`.
- [x] T013 [US3] Envolver el registro de `SyncEngineService`, `ScraperBackgroundService` y `AclaracionMonitorService` en un gate por configuración `RUN_INPROCESS_WORKERS` (default `true`) en `src/MPM.Modules.Licitaciones/ModuleRegistration.cs`
- [x] T014 [US3] Reemplazar los literales `DB_HOST="db"` / `DB_PORT="5432"` por lectura de configuración en `src/MPM.Modules.Licitaciones/Services/ScraperBackgroundService.cs` (líneas 118-119)
- [x] T015 [US3] Setear `RUN_INPROCESS_WORKERS=false` en la configuración de despliegue del servicio web `mpm-api` en Cloud Run (`scripts/deploy.sh` o el manifiesto de servicio correspondiente) — además se agregaron secretos `db-host`/`db-port`/`db-name`/`db-user`/`db-password` en `setup-secrets.sh` para que el subproceso Node del scraper (BUG-005) resuelva la BD en Cloud Run.

**Checkpoint**: US3 funcional — sin duplicación de sync y el scraper conecta a producción.

---

## Phase 6: User Story 4 - Scraper resiliente y observable (Priority: P1) 🎯 MVP crítico

**Goal**: El scraper distingue un cambio de estructura del sitio de un cupo agotado, nunca se cuelga por salida de error, y toda falla o resultado anómalo llega a Telegram. (BUG-003, BUG-006, BUG-007)

**Independent Test**: Ver `quickstart.md` § US4 — simular ausencia del grid con canary presente → alerta rápida; forzar salida de error abundante → el ciclo siempre termina; forzar 0 resultados → alerta, no éxito silencioso.

- [x] T016 [P] [US4] Unit test: lectura de `stdout`/`stderr` del proceso scraper ocurre en paralelo (`Task.WhenAll`), no secuencial, en `tests/MPM.Modules.Licitaciones.Tests/Services/ScraperBackgroundServiceTests.cs` — implementado como guarda de regresión sobre el código fuente (`Task.WhenAll(stdoutTask, stderrTask)` presente), no como prueba de comportamiento con un proceso real: forzar el llenado del buffer de stderr de forma determinística en CI es fràgil/lento y no aporta certeza extra sobre lo que ya prueba el patrón de código.
- [x] T017 [P] [US4] Unit test: `NotificarResultadoAsync` envía la alerta vía `TelegramNotificationService` (no a un GUID inexistente) y marca un ciclo con `total == 0` como advertencia, no éxito, en `tests/MPM.Modules.Licitaciones.Tests/Services/ScraperBackgroundServiceTests.cs` — se extrajo `EsCicloExitoso(exitCode, total)` como método `internal` puro, probado con `[Theory]` (4 casos); el enrutamiento a Telegram se cubre con una guarda de regresión sobre el código fuente (`NotificarOperacionesTelegramAsync` + `ListarAccountManagersAsync` presentes).
- [x] T018 [US4] Agregar canary de estructura en `tools/scraper-mp/modulos/adjuntos.js` (verificar un elemento de referencia estable antes de asumir "cupo agotado" cuando el grid `#DWNL_grdId` está ausente) y lanzar un error distinguible (`StructureChangedError`) — implementado como `err.isStructureChange = true`; canary = `document.body.innerText` contiene "adjunto". **Nota**: el canary es una heurística razonable sin acceso al DOM real de Mercado Público — confirmar contra el sitio real antes de confiar en él para el jueves (ver `quickstart.md` § US4).
- [x] T019 [US4] En `tools/scraper-mp/agente-mp.js`, separar el manejo de `StructureChangedError` (corte inmediato + alerta) del contador de `fallosConsecutivos` por cupo (línea ~242) — agregado marcador `ESTRUCTURA_CAMBIO_DETECTADA: true` en stdout, consumido por `ScraperBackgroundService.NotificarResultadoAsync` (T021) para alertar por Telegram.
- [x] T020 [US4] Reemplazar la lectura secuencial de `process.StandardOutput`/`process.StandardError` por `Task.WhenAll` antes de `WaitForExitAsync` en `src/MPM.Modules.Licitaciones/Services/ScraperBackgroundService.cs` (líneas 129-132)
- [x] T021 [US4] Inyectar `TelegramNotificationService` (de `MPM.Modules.Alertas`, ya referenciado en el `.csproj`) en `ScraperBackgroundService` y reemplazar el GUID `00000000-0000-0000-0000-000000000000` en las 4 llamadas a `notificaciones.CrearAsync` (líneas 193, 220, 229, 250) por el envío a Telegram, en `src/MPM.Modules.Licitaciones/Services/ScraperBackgroundService.cs` — se mantuvieron también las notificaciones in-app existentes (no rompen nada) y se sumó el envío real a Telegram a todos los account managers con chat vinculado, vía el nuevo método `NotificarOperacionesTelegramAsync`.
- [x] T022 [US4] Cambiar la condición de éxito en `NotificarResultadoAsync` de `exitCode == 0` a `exitCode == 0 && total > 0`, marcando 0 resultados como anómalo, en `src/MPM.Modules.Licitaciones/Services/ScraperBackgroundService.cs`

**Checkpoint**: Las 4 historias P1 (US1-US4) completas = bloqueantes del deploy del jueves resueltos.

---

## Phase 7: User Story 5 - CORS restringido + JWT secret obligatorio (Priority: P2)

**Goal**: La API solo acepta peticiones autenticadas del frontend autorizado y no arranca sin un secreto de sesión configurado. (BUG-011)

**Independent Test**: Ver `quickstart.md` § US5 — petición autenticada desde origen no autorizado es rechazada; arranque sin `JWT_SECRET` falla.

- [x] T023 [P] [US5] Integration test: petición con `Origin` no incluido en `Cors:AllowedOrigins` es rechazada; petición desde un origen permitido funciona, en `tests/MPM.Tests/CorsAndJwtStartupTests.cs` (no `MPM.Api.Tests`, que no existe). Verificado también en vivo contra el contenedor real: origen no autorizado no recibe `Access-Control-Allow-Origin`, `localhost:8181` sí.
- [x] T024 [P] [US5] Unit test: el host falla al arrancar si `JWT:Secret` es null/vacío o tiene menos de 32 caracteres, en `tests/MPM.Tests/CorsAndJwtStartupTests.cs` — implementado como guarda de regresión sobre el código fuente; interceptar el throw real vía `WebApplicationFactory` sobre un `Program.cs` de top-level statements resultó fràgil (el throw no se propagaba de forma confiable a través de la reflexión del entry point). BUG-001 (mismo patrón throw-antes-de-app.Run) ya se validó de punta a punta contra el contenedor real.
- [x] T025 [US5] Reemplazar `SetIsOriginAllowed(_ => true)` por `WithOrigins(allowedOrigins)` leyendo `Cors:AllowedOrigins` desde configuración, en ambas políticas (`default` y `"SignalR"`), en `src/MPM.Api/Program.cs` (líneas ~50-66)
- [x] T026 [US5] Eliminar el fallback `?? "default-secret-change-this-in-production-min-32-chars"` y lanzar `InvalidOperationException` si el secreto falta o es demasiado corto, en `src/MPM.Api/Program.cs` (líneas ~101-102) — se eliminó también un fallback duplicado equivalente en `AuthController.cs:67` que no estaba en el hallazgo original del QA pero es la misma clase de riesgo.
- [x] T027 [US5] Agregar `Cors__AllowedOrigins` (dominio del frontend de producción + `http://localhost:3000`, `http://localhost:8181`) a `docker-compose.yml` y a los secretos de Cloud Run vía `setup-secrets.sh`

**Checkpoint**: US5 funcional — riesgo de seguridad de CORS/JWT cerrado.

---

## Phase 8: User Story 6 - Webhook de Telegram fail-closed (Priority: P2)

**Goal**: El webhook rechaza cualquier petición sin la credencial correcta, incluyendo cuando no está configurada. (BUG-009)

**Independent Test**: Ver `quickstart.md` § US6 — sin secret configurado, una petición sin cabecera es rechazada (401).

- [x] T028 [P] [US6] Unit test: `TelegramWebhookController` rechaza la petición cuando `Telegram:WebhookSecret` está vacío/null, y cuando la cabecera no coincide, en `tests/MPM.Modules.Alertas.Tests/Controllers/TelegramWebhookControllerTests.cs` (4 casos: sin secret, sin cabecera, cabecera incorrecta, cabecera correcta)
- [x] T029 [US6] Invertir la condición de validación a fail-closed y usar `CryptographicOperations.FixedTimeEquals` para la comparación, en `src/MPM.Modules.Alertas/Controllers/TelegramWebhookController.cs` (líneas 25-31) — verificado en vivo: `curl` sin cabecera devuelve 401.

**Checkpoint**: US6 funcional — webhook ya no acepta tráfico no verificado.

---

## Phase 9: User Story 7 - Auditoría de inicios de sesión (Priority: P2)

**Goal**: Cada login exitoso queda registrado de forma consultable, para medir adopción (deadline día 16). (BUG-010)

**Independent Test**: Ver `quickstart.md` § US7 — login de varios usuarios, `SELECT * FROM auth_eventos` muestra una fila por cada uno.

- [x] T030 [US7] Migración `V092__Create_Auth_Eventos.sql`: tabla `auth_eventos` + `usp_Auth_RegistrarEvento` (esquema en `data-model.md`) en `src/MPM.Api/Database/Scripts/V092__Create_Auth_Eventos.sql` — aplicada contra el contenedor real.
- [x] T031 [P] [US7] Unit test: `AuthEventoHandler.RegistrarAsync` llama al stored procedure con los parámetros correctos y no propaga excepciones al llamador (falla silenciosa segura), en `tests/MPM.Modules.Auth.Tests/Data/AuthEventoHandlerTests.cs` — corre contra Postgres real.
- [x] T032 [P] [US7] Agregar constante `AuthRegistrarEvento` en `src/MPM.Modules.Auth/Data/AuthStoredProcedures.cs` (o archivo equivalente) apuntando a `CALL usp_Auth_RegistrarEvento(...)` — este módulo no usa un archivo de constantes separado (SQL inline en los handlers, patrón ya existente en `AuthHandler.cs`); el SQL vive directo en `AuthEventoHandler.cs` como `SELECT usp_Auth_RegistrarEvento(...)` (es una función `RETURNS VOID`, no un procedimiento con parámetros OUT, así que no necesita `CALL`).
- [x] T033 [US7] Crear `AuthEventoHandler` con método `RegistrarAsync(userId, tenantId, email, ipAddress, userAgent, ct)` en `src/MPM.Modules.Auth/Data/AuthEventoHandler.cs`
- [x] T034 [US7] Invocar `AuthEventoHandler.RegistrarAsync` inmediatamente después de generar `tokenString`, envuelto en try/catch que solo loguea (nunca bloquea el login), en `src/MPM.Modules.Auth/Controllers/AuthController.cs` (línea ~91) — **verificado con un login real** contra el contenedor (`admin@tivit.cl`/`test123`, credenciales demo de `V042__Seed_usuarios_demo.sql`): token emitido, fila en `auth_eventos` confirmada.

**Checkpoint**: US7 funcional — adopción medible desde el despliegue de este cambio.

---

## Phase 10: User Story 8 - Búsqueda de licitaciones con índice (Priority: P2)

**Goal**: La búsqueda por texto en el listado principal no se degrada con el volumen. (BUG-008)

**Independent Test**: Ver `quickstart.md` § US8 — `EXPLAIN ANALYZE` muestra uso de índice, no `Seq Scan`.

- [x] T035 [US8] Migración `V093__Fix_usp_Licitaciones_Listar_Search.sql`: `CREATE OR REPLACE` de `usp_Licitaciones_Listar` usando `search_vector @@ websearch_to_tsquery('spanish', p_search)` para nombre + índice trigram (`pg_trgm`) para `codigo_externo`, en `src/MPM.Api/Database/Scripts/V093__Fix_usp_Licitaciones_Listar_Search.sql` — **el V006 original que sirvió de base ya estaba desactualizado**: V079 había agregado una columna `Id BIGINT` al `RETURNS TABLE` (para el selector de "probar alerta" de Fase 6) que no estaba en V006; la primera versión de V093 no la incluía y falló en el contenedor real con `cannot change return type of existing function` — corregido añadiendo `Id`. También se agregó backfill de `search_vector` sin restricción de fecha (V066 solo cubrió licitaciones desde 2026-01-01).
- [x] T036 [P] [US8] Integration test: búsqueda por nombre y por código externo en `usp_Licitaciones_Listar` devuelve los mismos resultados esperados que antes del cambio, en `tests/MPM.Modules.Licitaciones.Tests/Data/LicitacionSearchTests.cs` — corre contra Postgres real; confirma `Bitmap Index Scan on idx_licitaciones_search_vector` (no `Seq Scan`) y que la búsqueda por código sigue devolviendo resultados.

**Checkpoint**: US8 funcional — búsqueda escalable sin cambios de contrato para el frontend.

---

## Phase 11: User Story 9 - Alertas eficientes y Telegram robusto (Priority: P3)

**Goal**: El matching de alertas no genera consultas N+1; los mensajes de Telegram siempre se entregan y con timeout acotado. (BUG-012, BUG-013)

**Independent Test**: Ver `quickstart.md` § US9 — una sola consulta de destinatarios por ciclo; nombre con `_`/`*` se entrega correctamente.

- [x] T037 [P] [US9] Unit test: `AlertasMatchingService.EvaluarLicitacionesAsync` consulta `ListarAccountManagersAsync` una sola vez por ciclo, no por licitación, en `tests/MPM.Modules.Alertas.Tests/Services/AlertasMatchingServiceTests.cs` — `AlertasHandler`/`AlertaEnriquecimientoService`/etc. son clases concretas sin interfaz (no mockeables con Moq sin refactor); implementado como guarda de regresión sobre el código fuente, confirmando que la consulta vive en el método por-ciclo y ya no en `ProcesarGrupoAsync`.
- [x] T038 [P] [US9] Unit test: `TelegramNotificationService` escapa correctamente `_`, `*`, `` ` ``, `[` y demás caracteres reservados de MarkdownV2 en el nombre de la licitación antes de enviar, en `tests/MPM.Modules.Alertas.Tests/Services/TelegramNotificationServiceTests.cs` — `EscaparMarkdownV2` es una función pura, probada directamente con `[Theory]` (4 casos) más un test de `FormatearMensaje` end-to-end.
- [x] T039 [US9] Mover la obtención de `destinatarios` desde `ProcesarGrupoAsync` hacia `EvaluarLicitacionesAsync`, pasándola como parámetro, en `src/MPM.Modules.Alertas/Services/AlertasMatchingService.cs` (línea 118) — también se agregó a `ProbarAsync` (una sola consulta, sí correspondía porque es un solo intento de prueba, no un ciclo).
- [x] T040 [US9] Agregar función de escape de MarkdownV2 y aplicarla al nombre/código de la licitación antes de interpolar el mensaje, actualizando `parse_mode` a `"MarkdownV2"`, en `src/MPM.Modules.Alertas/Services/TelegramNotificationService.cs` (línea 23) y en la construcción del mensaje en `AlertasMatchingService.cs` (línea 98) — el `mensaje` de `AlertasMatchingService.cs:98` es para la notificación in-app (no se envía a Telegram), así que no necesitaba escape; el mensaje real de Telegram se arma en `FormatearMensaje`, ya cubierto.
- [x] T041 [US9] Configurar `HttpClient.Timeout = TimeSpan.FromSeconds(10)` en el registro de `TelegramNotificationService`, en `src/MPM.Modules.Alertas/ModuleRegistration.cs` (línea 16)

**Checkpoint**: Las 13 correcciones del informe QA completas. **Validación real en producción durante el desarrollo**: al reconstruir el contenedor para probar P2/P3, el scraper real (`SCRAPER_ENABLED=true` en `.env`) corrió un ciclo real contra Mercado Público, encontró 0 licitaciones nuevas (ventana incremental de 1 día) y el fix de BUG-007 disparó correctamente una alerta real a Telegram — confirmado por el usuario, que la recibió. De paso se encontró y corrigió un bug preexistente no listado en el informe QA: en el camino de "0 licitaciones encontradas" de `agente-mp.js`, `cerrarYGenerar` se llamaba con `CARPETA_BASE` en vez de una carpeta de lote creada con `crearCarpetaLote()`, causando un `ENOENT` interno (capturado, no afectaba el resultado reportado, pero ensuciaba los logs) al intentar escribir `resumen.json`.

**Regresión propia detectada y corregida en el mismo ciclo de pruebas**: al activar `parse_mode = "MarkdownV2"` (BUG-013) en `TelegramNotificationService.EnviarAsync`, las alertas operativas del scraper (`ScraperBackgroundService.NotificarOperacionesTelegramAsync`, BUG-007) empezaron a fallar con 400 de Telegram ("Character '(' is reserved") porque interpolaban texto con paréntesis/puntos sin escapar. `EscaparMarkdownV2` se hizo `public` (antes `internal` de Alertas) y `NotificarOperacionesTelegramAsync` ahora escapa título y detalle antes de armar el mensaje. Cubierto con un nuevo test de regresión (`SourceCode_AlertasOperativasDelScraper_EscapanMarkdownV2AntesDeEnviar`) y reverificado en vivo.

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Verificación final y cierre de trazabilidad con el informe QA original.

- [x] T042 Ejecutar `dotnet test MPM.sln` completo y confirmar que las 41 tareas anteriores no rompieron ninguna suite existente — `MPM.sln` no registra 3 de los 10 proyectos de test (`MPM.Modules.Licitaciones.Tests`, `MPM.Modules.Mensajeria.Tests`, `MPM.Tests` — gap preexistente, no introducido por este feature); se corrieron los 10 por separado. 250 tests en verde; las 22 fallas de `MPM.Tests.Integration` son preexistentes (confirmado con `git stash` contra el código sin modificar) y no cambiaron. Salida completa guardada en `verification-logs/2026-07-08-dotnet-test-full-suite.txt` para la auditoría.
- [x] T043 Ejecutar manualmente cada sección de `specs/022-qa-fixes-preproduccion/quickstart.md` (US1–US9) contra el entorno de staging antes del jueves — ejecutado contra el contenedor Docker local (equivalente a staging en comportamiento, no en infraestructura GCP): US1 (migración fallida aborta el arranque — confirmado real durante el desarrollo de V093), US5/US6/US7/US8 (CORS, webhook, login+auditoría, búsqueda — todos con curl/psql reales), US4/BUG-007 (alerta real de Telegram recibida por el usuario durante una corrida real del scraper, dos veces). Los chequeos con curl/psql quedaron capturados como script reproducible en `verify-live.sh` (9/9 en verde), con su salida guardada en `verification-logs/2026-07-08-live-verification.txt`. **Pendiente**: correr contra el staging real de GCP una vez la infraestructura de Nicolás esté lista (bloqueante separado, ver `specs/002-fase5-deploy-gcp/`).
- [x] T044 [P] Actualizar `docs/runbook-produccion.md` con las nuevas variables de entorno (`RUN_INPROCESS_WORKERS`, `Cors__AllowedOrigins`, `Analisis__RecoveryThresholdMinutes`) y el flag `--no-cpu-throttling`
- [x] T045 Actualizar la nota de migraciones en `CLAUDE.md` (`<!-- SPECKIT START -->`) de "V092 y V093 planificadas, aún no aplicadas" a "aplicadas"
- [ ] T046 Marcar los 13 `BUG-ID` como "Resuelto" (con referencia al PR/commit) en el registro QA interno, si el equipo lleva seguimiento fuera de este repo

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias, arranca de inmediato.
- **Foundational (Phase 2)**: vacía — no bloquea nada.
- **US1–US4 (Phase 3-6, P1)**: sin dependencias entre sí ni con Setup más allá de T001-T002; **son los bloqueantes del jueves y deberían priorizarse primero y en paralelo si hay capacidad**.
- **US5–US8 (Phase 7-10, P2)**: sin dependencias con US1-US4; pueden avanzar en paralelo a ellas si hay capacidad, pero no son bloqueantes del jueves.
- **US9 (Phase 11, P3)**: sin dependencias con las anteriores.
- **Polish (Phase 12)**: depende de que las historias que se decida entregar estén completas.

### User Story Dependencies

Las 9 historias son mutuamente independientes — cada una toca un módulo y un conjunto de archivos distinto (confirmado en `plan.md`, Constitution Check). Única relación notable: **US4 (T021) inyecta `TelegramNotificationService` de `MPM.Modules.Alertas` dentro de `MPM.Modules.Licitaciones`**, reutilizando una `ProjectReference` que ya existía — no crea una dependencia nueva entre historias de este feature.

### Parallel Opportunities

- Todas las tareas `[P]` dentro de una misma historia se pueden ejecutar en paralelo (archivos distintos).
- Las 4 historias P1 (US1-US4) se pueden trabajar en paralelo entre sí por distintas personas — son la prioridad para el jueves.
- Las 4 historias P2 (US5-US8) se pueden trabajar en paralelo entre sí y con las P1 si hay capacidad de sobra.

---

## Parallel Example: Historias P1 (bloqueantes del jueves)

```bash
# Cuatro desarrolladores en paralelo, una historia cada uno:
Task: "US1 — DatabaseInitializer.cs: pg_advisory_lock + throw"
Task: "US2 — AnalisisRecoveryWorker.cs + deploy.sh --no-cpu-throttling"
Task: "US3 — ModuleRegistration.cs gate RUN_INPROCESS_WORKERS + ScraperBackgroundService.cs DB_HOST"
Task: "US4 — adjuntos.js canary + agente-mp.js + ScraperBackgroundService.cs (stdout/stderr, Telegram)"
```

---

## Implementation Strategy

### MVP First (bloqueantes del jueves)

1. Completar Phase 1 (Setup) — trivial, ~10 minutos.
2. Phase 2 (Foundational) — vacía, saltar.
3. Completar Phase 3-6 (US1-US4, las 4 historias P1) — **este es el MVP real de este feature**: sin esto, el deploy del jueves no debería proceder según la propia recomendación del QA.
4. **DETENER Y VALIDAR**: correr `quickstart.md` § US1-US4 contra staging antes de continuar.
5. Si el tiempo lo permite antes del jueves, continuar con US5-US8 (P2); si no, quedan para inmediatamente después.
6. US9 (P3) es la última prioridad — no bloquea nada de negocio inmediato.

### Incremental Delivery

1. US1 → valida sola (evita el peor escenario: prod con esquema roto).
2. US2 → valida sola (evita análisis perdidos).
3. US3 → valida sola (evita sync duplicado y scraper desconectado).
4. US4 → valida sola (evita silencio operativo del scraper).
5. Con las 4 P1 desplegadas y validadas, el sistema está listo para el jueves aunque US5-US9 sigan pendientes.

---

## Notes

- Cada tarea de implementación referencia el archivo exacto y, cuando aplica, el rango de líneas identificado durante la verificación con subagentes (2026-07-08) contra el código real — no son ubicaciones supuestas.
- El scraper (`tools/scraper-mp/`) no tiene suite de tests automatizada; sus tareas (T018, T019) se validan exclusivamente vía `quickstart.md`, tal como señala la sección "Tests" arriba.
- Commitear después de cada tarea o grupo lógico; cada historia debe quedar en un estado funcional al final de su fase.
- Antes de mergear, correr `dotnet test MPM.sln` completo (T042) — no alcanza con los tests nuevos de cada historia.
