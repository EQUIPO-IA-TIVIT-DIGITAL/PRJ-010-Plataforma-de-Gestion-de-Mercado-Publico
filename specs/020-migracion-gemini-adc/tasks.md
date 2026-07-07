# Tasks: Migración de Gemini de API Key a ADC/Vertex AI

**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Estado**: ✅ Implementado y validado end-to-end (2026-07-06)

## Hecho

- [x] T001 Investigación en vivo: confirmado que `generativelanguage.googleapis.com` rechaza tanto la key actual (`401 ACCESS_TOKEN_TYPE_UNSUPPORTED`) como un token ADC estándar (`403 ACCESS_TOKEN_SCOPE_INSUFFICIENT`); Vertex AI (`aiplatform.googleapis.com`) acepta el token ADC (`200 OK`).
- [x] T002 `Google.Apis.Auth` agregado a `MPM.Shared.csproj`.
- [x] T003 `src/MPM.Shared/Services/GoogleAdcTokenProvider.cs` creado — obtiene/cachea el token ADC, scope `cloud-platform`, método `GetAccessTokenAsync` marcado `virtual` para poder mockearse en tests.
- [x] T004 `GoogleAdcTokenProvider` registrado como singleton en `Program.cs` (servicio web y modo worker).
- [x] T005 `SinonimosIaService.cs` (Alertas) reescrito: endpoint Vertex AI, body camelCase (`generationConfig`), `Authorization: Bearer {token}` en vez de `?key=`.
- [x] T006 `GeminiService.cs` (Analisis) reescrito completo:
  - Endpoint Vertex AI + ADC, mismo patrón que T005.
  - **Eliminada** la File API de la Developer API (`AnalyzePdfViaFileApiAsync`, `EsperarArchivoActivoAsync`) — no existe en Vertex AI.
  - **Reemplazada** por `fileData.fileUri = gs://...` cuando el documento ya está en GCS (caso de producción) — más simple y sin el polling de estado que tenía la File API.
  - `inlineData` (base64) se mantiene como único camino para storage local.
- [x] T007 Eliminado el parámetro `geminiApiKey` de toda la cadena de llamadas: `IAnalisisBackgroundService`, `AnalisisBackgroundService`, `AnalisisService.AnalizarAsync`/`ChatAsync`, `AnalisisController` (se quitó también la propiedad `GeminiApiKey` y la dependencia de `IConfiguration` que solo se usaba para eso).
- [x] T008 Tests de `GeminiServiceTests.cs` actualizados al nuevo constructor/firma — incluye tests nuevos verificando: endpoint de Vertex AI correcto, header `Bearer` presente, `fileData.fileUri` usado cuando hay `gcsUri`, `inlineData` usado cuando no.
- [x] T009 Config: `Gemini:ApiKey`/`GEMINI_API_KEY` eliminados de `appsettings.json`/`docker-compose.yml`/`.env.example`; agregado `Vertex:Region` (default `us-central1`).
- [x] T010 `docker-compose.yml`: monta `ADC_CREDENTIALS_PATH` (variable de entorno del desarrollador, apunta a `application_default_credentials.json` de su `gcloud auth application-default login`) como `/app/adc-credentials.json`, con `GOOGLE_APPLICATION_CREDENTIALS` apuntando ahí.
- [x] T011 `002-fase5-deploy-gcp/solicitud-recursos-cloud-run.md` actualizado: se agrega `roles/aiplatform.user` a la lista de roles pendientes para `mpm-api-sa`/`mpm-jobs-sa`.
- [x] T012 `scripts/deploy.sh`/`scripts/setup-secrets.sh`: quitado todo rastro de `SECRET_GEMINI`/`gemini-api-key`; `GOOGLE_CLOUD_PROJECT` y nuevo `Vertex__Region` agregados a las env vars del deploy.
- [x] T013 **Validado end-to-end contra Gemini real vía Vertex AI + ADC**: se creó una alerta con keyword "data center" y `SinonimosIaService` devolvió 8 sinónimos reales y coherentes ("centro de procesamiento de datos", "infraestructura de TI", "sala de servidores", etc.) — sin ninguna API key configurada, usando únicamente la identidad ADC de la cuenta.

## No hecho / riesgo aceptado

- [ ] T014 **`GeminiService` (análisis de PDF) no se probó end-to-end** — requeriría subir un PDF real vía el frontend de Análisis y esperar el resultado completo. Se validó por code review + compilación + el mecanismo de auth idéntico ya probado en T013, pero no se ejecutó el pipeline completo de análisis de un acta real en esta sesión.
- [ ] T015 El comportamiento de `fileData.fileUri` con Vertex AI para PDFs escaneados (el bug original que motivó la File API, ver memoria `feedback-scraper-bugs` Bug 3) no se validó específicamente contra un PDF escaneado real — se asume que Vertex AI maneja igual o mejor los PDFs de imagen vía GCS que la Developer API vía File API, pero no hay una prueba directa todavía.
- [ ] T016 Otros desarrolladores del equipo necesitan correr `gcloud auth application-default login` una vez y configurar `ADC_CREDENTIALS_PATH` en su `.env` local — no está automatizado, es un paso manual documentado en `.env.example`.
