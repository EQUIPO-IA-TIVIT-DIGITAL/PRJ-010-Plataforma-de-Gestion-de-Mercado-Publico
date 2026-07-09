# Quickstart: Validación de Corrección de Hallazgos QA Pre-Producción

Guía de validación manual, una sección por hallazgo. Asume el stack levantado localmente (`docker compose up --build`) salvo que se indique explícitamente "en Cloud Run" (requiere el entorno de staging de `specs/002-fase5-deploy-gcp/`).

> **Automatizado**: los chequeos de US1, US5, US6, US7 y US8 (los que se pueden verificar con `curl`/`psql` sin interacción humana) están capturados en `verify-live.sh` — correrlo con `bash specs/022-qa-fixes-preproduccion/verify-live.sh` reproduce esas secciones de una sola vez. Su salida (2026-07-08, 9/9 en verde) está guardada en `verification-logs/`. US2, US3, US4 y US9 siguen siendo manuales (requieren interrumpir procesos, forzar concurrencia, o revisar Telegram/UI).

## Prerrequisitos

- `docker compose up --build` corriendo (API `:5001`, Web `:8181`, DB `:5433`).
- Acceso a un proyecto de staging en GCP con los Cloud Run Jobs de sync/scraper configurados, para los casos que requieren validarse "en Cloud Run".
- Un usuario de prueba con licitaciones y al menos un workspace de análisis existente.

## US1 — BUG-001: Migraciones fail-fast

1. Crear un archivo `.sql` temporal en `src/MPM.Api/Database/Scripts/` con sintaxis inválida (p. ej. `SELECT * FORM tabla_inexistente;`), numerado por encima de V093.
2. `docker compose up --build api` (o `dotnet run --project src/MPM.Api`).
3. **Esperado**: el proceso termina con error visible en logs; `curl http://localhost:5001/health` no responde 200 (conexión rechazada o el contenedor sale).
4. Eliminar el archivo temporal, volver a levantar y confirmar arranque normal.

## US2 — BUG-002: Análisis sobrevive a interrupciones

1. Subir un PDF a un workspace de análisis vía la UI (`/analisis/:id`).
2. Antes de que termine (~20-90s), matar el proceso de la API (`docker compose kill api` o equivalente) simulando pérdida de instancia.
3. Levantar la API de nuevo.
4. Esperar el intervalo de `AnalisisRecoveryWorker` (`Analisis:RecoveryThresholdMinutes`, bajar a 1 minuto en config local para la prueba).
5. **Esperado**: el workspace pasa de `analizando` a `completado` (o `error` si el documento era inválido) sin volver a subir el archivo.

## US3 — BUG-004 / BUG-005: Sync/scraper sin duplicar, DB_HOST configurable

1. Con `RUN_INPROCESS_WORKERS=false` en el `.env` de un entorno que simule Cloud Run (o directamente en staging), levantar la API y, por separado, ejecutar el Cloud Run Job de sync manualmente (`WORKER_MODE=sync`).
2. Revisar logs: el servicio web NO debe loguear ejecuciones de `SyncEngineService`/`ScraperBackgroundService`.
3. En Docker Compose local (`RUN_INPROCESS_WORKERS` sin setear o `true`), confirmar que el comportamiento actual (workers in-process) sigue funcionando sin regresión.
4. Ejecutar el scraper como Cloud Run Job en staging y confirmar en logs que conecta exitosamente a Cloud SQL (no a `db`).

## US4 — BUG-003 / BUG-006 / BUG-007: Resiliencia del scraper

1. Renombrar temporalmente el `id` del grid en una copia local de prueba del HTML (o mockear el DOM) para simular un cambio de estructura del sitio.
2. Ejecutar el scraper y confirmar que se detiene en minutos (no ~3 horas) y que llega una alerta a Telegram distinguible de "cupo agotado".
3. Ejecutar un ciclo del scraper forzando salida de error abundante (p. ej. variable de entorno de debug de Playwright al máximo) y confirmar que el ciclo siempre termina.
4. Forzar un ciclo con 0 licitaciones nuevas y confirmar que llega una alerta de Telegram (no se reporta como éxito silencioso).

## US5 — BUG-011: CORS + JWT secret

1. Desde una página HTML servida en otro origen (`file://` o un servidor local en otro puerto), intentar un `fetch` autenticado contra `http://localhost:5001/api/...` con `credentials: 'include'`.
2. **Esperado**: la petición es rechazada por CORS (bloqueada en el navegador).
3. Confirmar que el frontend real (`:8181` o `:3000`) sigue autenticando con normalidad.
4. Levantar la API sin `JWT_SECRET` en el `.env` y confirmar que el arranque falla con un mensaje claro.

## US6 — BUG-009: Webhook de Telegram fail-closed

1. Sin `Telegram:WebhookSecret` configurado, enviar `curl -X POST http://localhost:5001/api/v1/telegram/webhook -d '{}'` sin cabecera de secret.
2. **Esperado**: `401 Unauthorized`.
3. Configurar el secret, repetir la petición con la cabecera correcta y confirmar que se procesa (o con una incorrecta y confirmar rechazo).

## US7 — BUG-010: Auditoría de login

1. Iniciar sesión con 2-3 usuarios de prueba distintos.
2. Consultar `SELECT * FROM auth_eventos ORDER BY created_at DESC;` directamente en la base de datos.
3. **Esperado**: una fila por cada login exitoso, con `user_id`, `email` y `created_at` correctos.

## US8 — BUG-008: Búsqueda con índice

1. Cargar (o usar) una base con volumen representativo de licitaciones (cientos-miles de filas).
2. Ejecutar `EXPLAIN ANALYZE SELECT * FROM usp_Licitaciones_Listar(1, 20, 'construcción', NULL, ...);` (ajustar parámetros según la firma real) en la base de datos.
3. **Esperado**: el plan de ejecución muestra uso de `Bitmap Index Scan` sobre `search_vector` (o el índice trigram para búsquedas por código), no un `Seq Scan` completo.
4. Confirmar en la UI que buscar por nombre y por código externo siguen devolviendo resultados correctos.

## US9 — BUG-012 / BUG-013: Alertas eficientes y mensajes robustos

1. Crear una regla de alerta con una palabra clave común y forzar un ciclo de sync con varias licitaciones nuevas que coincidan.
2. Con logging de consultas SQL activado, confirmar que `ListarAccountManagersAsync` se llama una sola vez para todo el ciclo (no una vez por licitación).
3. Crear/forzar una licitación de prueba con un nombre que incluya `_` y `*` (p. ej. `"Compra_de_equipos * urgente"`), disparar su alerta y confirmar que el mensaje llega correctamente a un chat de Telegram de prueba.
4. (Opcional, si es reproducible) Simular un endpoint de Telegram lento y confirmar que el envío no excede ~10s antes de darse por fallido y registrarlo.
