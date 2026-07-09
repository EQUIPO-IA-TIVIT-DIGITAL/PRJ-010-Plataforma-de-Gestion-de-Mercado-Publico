# Phase 0: Research — Corrección de Hallazgos QA Pre-Producción

Todas las decisiones parten de código ya leído en el repo (no hay dependencias externas nuevas ni stack a evaluar); "research" aquí consiste en confirmar el mecanismo correcto dentro de las convenciones existentes del proyecto (Constitución: stored procedures first, migraciones embebidas, sin ORM).

## R1 — BUG-001: Arranque a prueba de fallos de migración

**Decisión**: En `DatabaseInitializer.cs`, (a) adquirir `pg_advisory_lock(<hash fijo>)` al inicio de `InitializeAsync()` sobre la misma conexión que aplica las migraciones, liberarlo en un `finally`; (b) eliminar el `catch (Exception ex) { logger.LogError(...) }` silencioso de la línea 56-59 y dejar que la excepción se propague (o hacer `throw` explícito tras loguear), de forma que `Program.cs` no llegue a `app.Run()` si una migración falla.

**Rationale**: `pg_advisory_lock` es nativo de PostgreSQL (sin dependencia nueva), y es el mecanismo estándar para coordinar un "solo un ejecutor a la vez" entre instancias que arrancan concurrentemente en Cloud Run. Propagar la excepción es la forma idiomática en .NET de que `Program.cs` (o el propio host) aborte el arranque — no se necesita lógica de salida (`Environment.Exit`) adicional si `IHostedService`/`Main` no atrapa la excepción en ningún nivel superior.

**Alternatives considered**: Un archivo lock en disco — descartado porque no funciona entre instancias de Cloud Run (sistema de archivos efímero, no compartido). Un flag en una tabla propia sin `SELECT ... FOR UPDATE`/advisory lock — descartado por condición de carrera entre el `SELECT` y el `UPDATE`.

## R2 — BUG-002: Análisis debe sobrevivir a CPU throttling / reinicio

**Decisión**: Dos capas, sin agregar tablas nuevas:
1. **Mitigación de piso (deploy)**: agregar `--no-cpu-throttling` a la llamada `gcloud run deploy` de `mpm-api` en `scripts/deploy.sh` (`deploy_api()`), tal como propuso el QA como fix interino.
2. **Fix estructural (código)**: agregar un `IHostedService` nuevo, `AnalisisRecoveryWorker`, que corre cada ~60s y: consulta `usp_AnalisisWorkspaces_Listar(p_estado='analizando')` (ya existe, soporta filtro por estado), descarta los que ya tienen un resultado en `usp_AnalisisResultados_ObtenerPorWorkspace` (análisis que sí terminó pero el estado no se actualizó a "completado" por otra causa — caso raro, se corrige aparte), y para los que llevan más de un umbral configurable (p. ej. `Analisis:RecoveryThresholdMinutes`, default 5) sin resultado, vuelve a invocar el mismo pipeline de `AnalisisBackgroundService.ProcessAnalisisAsync` reobteniendo el documento vía `usp_AnalisisDocumentos_Listar(workspace_id)` (tomando el más reciente).

**Rationale**: `AnalisisService.cs:99` ya hace `ActualizarEstadoAsync(workspaceId, "analizando", ct)` de forma **síncrona, antes** de encolar el `Task.Run` — es decir, el estado "esto está pendiente de completarse" ya es durable en `analisis_workspaces.estado` desde antes de este feature. El bug no es la falta de persistencia del encargo, sino la falta de un mecanismo que vuelva a intentar el trabajo si el fire-and-forget muere. Un poller que reutiliza el estado ya persistido y los stored procedures ya existentes cumple la Constitución (nada de tablas de cola nuevas, nada de mensajería nueva) y resuelve el caso real: instancia se recicla → el próximo poll (de la misma instancia si vuelve, o de otra) retoma el workspace huérfano.

**Alternatives considered**: Cola durable dedicada (tabla `analisis_cola` + `FOR UPDATE SKIP LOCKED`) — es el diseño "correcto" a largo plazo que el propio QA sugiere, pero es más trabajo del necesario para el jueves cuando el estado ya persistido resuelve el 100% de los casos de recuperación con cambios menores. Se documenta como mejora futura en `spec.md` si se requiere paralelismo real entre workers (hoy el análisis es secuencial por naturaleza — un documento a la vez por workspace). Rediseño a consumidor Pub/Sub (mencionado en el TODO de `Program.cs:25-28`) — descartado para este feature por alcance/tiempo; es la opción de "Cloud Run Job dedicado" y requeriría separar `Analisis` en su propio worker, un cambio de infraestructura fuera del alcance de una corrección de bug.

## R3 — BUG-003: Scraper debe distinguir cambio de estructura de cupo agotado

**Decisión**: En `tools/scraper-mp/modulos/adjuntos.js`, agregar un "canary" — antes de asumir que la ausencia del grid (`#DWNL_grdId`) es "cupo agotado", verificar la presencia de al menos un elemento de referencia estable de la página (p. ej. el título de la sección o el breadcrumb) que confirme que la página cargó correctamente. Si el canary está presente pero el grid no, es un cambio de estructura real → lanzar un error distinguible (`StructureChangedError`) en vez de retornar `null` silenciosamente. En `agente-mp.js:242`, separar el contador `fallosConsecutivos` (cupo, se resuelve con cooldown) de un nuevo camino para `StructureChangedError` que corta el ciclo de inmediato (sin reintentos de 20 min) y dispara una alerta a Telegram (ver R5, reutiliza el mismo servicio de notificación).

**Rationale**: El QA propone exactamente esto ("canary del grid + circuit breaker + alerta a Telegram"); es la corrección de menor riesgo porque no cambia el flujo de éxito, solo agrega una rama de detección explícita.

**Alternatives considered**: Reintentar con un selector de fallback en vez de fallar — descartado porque enmascara el problema real (el QA ya nota que los selectores posicionales son frágiles; un fallback silencioso solo pospone la detección).

## R4 — BUG-004: Sincronización duplicada web + Job

**Decisión**: En `ModuleRegistration.cs` de `MPM.Modules.Licitaciones`, envolver los tres `AddHostedService` (`SyncEngineService`, `ScraperBackgroundService`, `AclaracionMonitorService`) en un `if` que lee una nueva variable de entorno `RUN_INPROCESS_WORKERS` (default `true` para no romper Docker Compose local; se setea explícitamente a `false` en el deploy de `mpm-api` a Cloud Run). Esto es independiente y complementario al `WORKER_MODE` que ya existe en `Program.cs` para los Cloud Run Jobs — `WORKER_MODE` controla qué hace el proceso cuando se ejecuta *como Job* (una pasada y sale); `RUN_INPROCESS_WORKERS` controla si el servicio web normal, además, arranca sus propios timers en paralelo. `AclaracionMonitorService` (que hoy no tiene ningún camino de `WORKER_MODE`) queda incluido en el mismo gate; si se requiere que siga corriendo en producción, se agrega como un tercer Cloud Run Job + Cloud Scheduler en `specs/002-fase5-deploy-gcp/` (fuera de alcance de este feature de código — se deja como riesgo documentado, ya señalado por el QA en su observación #4).

**Rationale**: Mínimo cambio posible que cierra la duplicación sin tocar el mecanismo de Jobs ya construido; conserva el comportamiento actual en local (Docker Compose) por defecto.

## R5 — BUG-005: DB_HOST hardcodeado

**Decisión**: En `ScraperBackgroundService.cs:118-119`, reemplazar los literales `"db"` / `"5432"` por `configuration["DB_HOST"]` / `configuration["DB_PORT"]`, igual que ya se hace en las líneas contiguas (120-122) para `DB_NAME`/`DB_USER`/`DB_PASSWORD`. En Cloud Run Jobs, `DB_HOST` apunta a la IP privada de Cloud SQL (vía Secret Manager/env, ya provisto por `setup-secrets.sh` según `docs/runbook-produccion.md`); en Docker Compose local sigue siendo `"db"` porque así está en el `.env`/`docker-compose.yml`.

**Rationale**: Consistencia con el resto del bloque, cero riesgo — es un cambio de una constante a una lectura de config ya disponible.

## R6 — BUG-006: Deadlock potencial stdout/stderr

**Decisión**: Reemplazar la lectura secuencial (`await LeerLineasAsync(stdout); await LeerLineasAsync(stderr);`) por `var stdoutTask = LeerLineasAsync(process.StandardOutput, ct); var stderrTask = LeerLineasAsync(process.StandardError, ct); await Task.WhenAll(stdoutTask, stderrTask); await process.WaitForExitAsync(ct);`.

**Rationale**: Es la corrección textual que propone el QA; patrón estándar de .NET para evitar el deadlock clásico de buffers de pipe llenos.

## R7 — BUG-007: Alertas del scraper a un buzón inexistente

**Decisión**: Reemplazar el GUID `00000000-0000-0000-0000-000000000000` en las llamadas a `notificaciones.CrearAsync` (líneas 193, 220, 229, 250) por un envío directo vía `TelegramNotificationService` de `MPM.Modules.Alertas`, inyectado en `ScraperBackgroundService`. Adicionalmente, en `NotificarResultadoAsync`, cambiar la condición de éxito de "solo `exitCode == 0`" a "`exitCode == 0` **y** `total > 0`", marcando un ciclo con 0 resultados como advertencia, no éxito.

**Rationale**: Resuelve el problema real (nadie ve la alerta) reutilizando la única vía de notificación externa que ya funciona en el sistema (Telegram), en vez de arreglar el flujo in-app que de todos modos no llega a nadie fuera de la aplicación. Confirmado en el `.csproj`: `MPM.Modules.Licitaciones` ya tiene `ProjectReference` a `MPM.Modules.Alertas` (usado hoy para el pipeline de matching de alertas tras el sync), así que esta dependencia no es nueva ni viola el árbol de módulos actual — no requiere abstracción adicional en `MPM.Shared`.

## R8 — BUG-008: Búsqueda no usa el índice de texto

**Decisión**: Migración `V093__Fix_usp_Licitaciones_Listar_Search.sql`, `CREATE OR REPLACE FUNCTION usp_Licitaciones_Listar(...)` reemplazando la condición `ILIKE '%' || p_search || '%'` por `l.search_vector @@ websearch_to_tsquery('spanish', p_search)` para el nombre, más un índice trigram (`pg_trgm`, `CREATE INDEX ... USING gin (codigo_externo gin_trgm_ops)`) para mantener la búsqueda por código exacto/parcial eficiente (el código externo no es texto libre, no aplica tsvector). Paginación: se deja `OFFSET` para este fix (cambiar a keyset pagination es una mejora de mayor alcance que no está entre los hallazgos críticos del jueves; se documenta como no-objetivo explícito).

**Rationale**: Reutiliza exactamente el patrón ya validado y en producción en `usp_Licitaciones_BuscarNatural` (V067) para el nombre; agrega trigram solo donde tsvector no aplica (código). Evita mantener dos stored procedures de listado divergentes — se corrige el que expone `/api/licitaciones` (el que usan todas las pantallas hoy) en vez de migrar el frontend a un segundo endpoint.

**Alternatives considered**: Migrar el frontend para usar `usp_Licitaciones_BuscarNatural` en vez de arreglar el listado principal — descartado porque ese endpoint fue diseñado para búsqueda en lenguaje natural (feature `018-buscador-inteligente-nl`), no para el filtro simple por código/nombre del listado; mezclar ambos casos de uso complica el contrato del endpoint nuevo sin necesidad.

## R9 — BUG-009: Webhook de Telegram fail-open

**Decisión**: En `TelegramWebhookController.cs`, invertir la condición: si `secretEsperado` está vacío/null, rechazar siempre (`return Unauthorized()`) en vez de omitir la validación. Cambiar la comparación de `secretRecibido != secretEsperado` a `CryptographicOperations.FixedTimeEquals(...)` sobre los bytes UTF-8 de ambos valores.

**Rationale**: Fix textual propuesto por el QA; cierra el fail-open y agrega comparación en tiempo constante para evitar timing attacks sobre el secreto.

## R10 — BUG-010: Sin auditoría de login

**Decisión**: Migración `V092__Create_Auth_Eventos.sql`: tabla `auth_eventos` (`id`, `user_id`, `tenant_id`, `email`, `ip_address`, `user_agent` nullable, `created_at`) + `usp_Auth_RegistrarEvento(p_user_id, p_tenant_id, p_email, p_ip_address, p_user_agent, OUT p_error_msg)`. En `AuthController.cs`, inmediatamente después de generar el `tokenString` (línea ~91), invocar el nuevo handler de forma "fire-and-forget seguro" (con try/catch propio que solo loguea, sin bloquear ni fallar el login si el registro de auditoría falla) — el login exitoso nunca debe depender de que la auditoría tenga éxito.

**Rationale**: Coincide con la propuesta del propio QA ("vía más rápida y correcta"); usa el patrón `usp_*` + Dapper estándar del proyecto, sin Prometheus (correctamente descartado por el QA por la cardinalidad por-usuario).

## R11 — BUG-011: CORS abierto + secreto JWT por defecto

**Decisión**: En `Program.cs`, reemplazar `SetIsOriginAllowed(_ => true)` (ambas políticas, default y `"SignalR"`) por `WithOrigins(allowedOrigins)` leyendo una lista desde configuración (`Cors:AllowedOrigins`, nueva entrada en `.env`/`appsettings`, con el dominio del frontend de producción + `http://localhost:3000`/`:8181` para desarrollo). Para el secreto JWT, eliminar el fallback `?? "default-secret-change-this-in-production-min-32-chars"` y en su lugar lanzar una excepción de arranque (`InvalidOperationException("JWT:Secret no configurado")`) si `jwtSection["Secret"]` es null/vacío o tiene menos de 32 caracteres.

**Rationale**: Ambos son fixes textuales propuestos por el QA. La allow-list de orígenes es configurable (no hardcodeada) para no romper el patrón multi-entorno (local/staging/prod) ya usado en el resto del `.env`.

## R12 — BUG-012: N+1 en matching de alertas

**Decisión**: En `AlertasMatchingService.cs`, mover `var destinatarios = (await handler.ListarAccountManagersAsync(ct)).ToList();` desde `ProcesarGrupoAsync` (invocado por licitación) hacia `EvaluarLicitacionesAsync` (el método de nivel de ciclo), pasando `destinatarios` como parámetro a `ProcesarGrupoAsync`.

**Rationale**: Fix mecánico propuesto por el QA; no cambia el resultado, solo el número de consultas.

## R13 — BUG-013: Markdown sin escapar + sin timeout en Telegram

**Decisión**: En `TelegramNotificationService.cs` y en el punto donde `AlertasMatchingService.cs:98` construye `mensaje`, aplicar una función de escape de MarkdownV2 (`_`, `*`, `` ` ``, `[`, `]`, `(`, `)`, `~`, `` ` ``, `>`, `#`, `+`, `-`, `=`, `|`, `{`, `}`, `.`, `!` según la spec de Telegram) al nombre/código de la licitación antes de interpolarlo, y actualizar `parse_mode` a `"MarkdownV2"` (o alternativamente remover `parse_mode` y enviar texto plano — se prefiere MarkdownV2 escapado para conservar el formato ya usado en el resto del mensaje). En `ModuleRegistration.cs:16`, agregar `.AddHttpClient<TelegramNotificationService>(c => c.Timeout = TimeSpan.FromSeconds(10))`.

**Rationale**: Fix textual propuesto por el QA; MarkdownV2 escapado se prefiere sobre texto plano porque conserva el formato visual (negritas en el nombre de la licitación) que el equipo probablemente quiere mantener — a confirmar en `quickstart.md` con una prueba visual antes de cerrar la tarea.

## Resumen de migraciones nuevas

| Archivo | Bug | Contenido |
|---|---|---|
| `V092__Create_Auth_Eventos.sql` | BUG-010 | Tabla `auth_eventos` + `usp_Auth_RegistrarEvento` |
| `V093__Fix_usp_Licitaciones_Listar_Search.sql` | BUG-008 | `CREATE OR REPLACE` de `usp_Licitaciones_Listar` con tsvector + índice trigram para código |

Ningún otro bug requiere migración.
