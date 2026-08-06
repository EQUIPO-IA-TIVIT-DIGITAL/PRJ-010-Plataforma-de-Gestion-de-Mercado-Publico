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

> ✅ **Reconciliado 2026-08-03 contra logs reales de producción** (`gcloud logging read` sobre `tivit-cu010`, 30 días): T014 y T015 quedan confirmados por evidencia directa de uso real, no solo por code review.

- [x] T014 **Confirmado 2026-08-03**: 50+ análisis completados exitosamente en producción en los últimos 30 días (`"Análisis completado para workspace N, resultado M"`), todos vía `https://us-central1-aiplatform.googleapis.com/.../gemini-2.5-pro:generateContent` con `200 OK`. El pipeline completo (subida → GCS → Gemini vía Vertex AI/ADC → resultado guardado) corre de punta a punta sin intervención.
- [x] T015 **Confirmado 2026-08-03**: los logs muestran `fileData.fileUri` (`gcsUri=gs://tivit-cu010-mpm-adjuntos/...`) usado contra actas y resoluciones reales — ej. "Resolución de Acta de Adjudicación.pdf", "REX N°280 DECLÁRESE INADMISIBLE..." — documentos oficiales chilenos que típicamente son escaneos firmados, no PDFs de texto plano. 0 errores de Gemini/Vertex AI en 30 días (el único error encontrado en el período, 2026-07-10, es un `DirectoryNotFoundException` de storage local en un documento legado, no relacionado a ADC/Vertex).
- [ ] T016 Otros desarrolladores del equipo necesitan correr `gcloud auth application-default login` una vez y configurar `ADC_CREDENTIALS_PATH` en su `.env` local — no está automatizado, es un paso manual documentado en `.env.example`. Sigue siendo el único pendiente real de esta spec, y es de bajo impacto (onboarding, no producción).
