# Feature Specification: Migración de Gemini de API Key a ADC/Vertex AI

**Feature Branch**: `020-migracion-gemini-adc`
**Created**: 2026-07-06
**Status**: Planned
**Impacto**: Alto (afecta Análisis y Alertas, ambos módulos que ya están en producción/demo) | **Complejidad**: Media | **Depende de**: —

## Contexto

Al probar `003-fase6-alertas-keywords` contra el portal real, se descubrió que `GEMINI_API_KEY` en `.env` no es una API key válida de Gemini — Google la rechaza con `ACCESS_TOKEN_TYPE_UNSUPPORTED` (confirmado con un curl directo contra `generativelanguage.googleapis.com`, sin pasar por MPM). El equipo heredó el proyecto y no sabe dónde quedó la key original ni quién la administra.

En vez de generar/gestionar una nueva API key (otro secreto más que rotar, guardar y no perder), se decide migrar toda la integración con Gemini a **Application Default Credentials (ADC)** sobre **Vertex AI**, usando la identidad de la cuenta/Service Account en vez de un secreto de texto plano. Se confirmó viable: con la cuenta `matias.mendez@tivit.com` y un token ADC, una llamada a `{region}-aiplatform.googleapis.com` respondió `200 OK` con contenido real de Gemini — la propia `generativelanguage.googleapis.com` (Developer API) rechazó el mismo token con `ACCESS_TOKEN_SCOPE_INSUFFICIENT`, confirmando que la API correcta para autenticación por cuenta es Vertex AI, no la Developer API.

**Por qué ahora y no después**: esto se va a probar (demo del jueves incluye Alertas, que depende de Gemini para sinónimos) y no se puede dejar con una credencial que nadie sabe de dónde salió ni cómo reponer — es un riesgo operativo, no solo un bug puntual.

## Alcance

Afecta **dos módulos existentes**, no solo el nuevo:
- `MPM.Modules.Analisis` — `GeminiService` (análisis de PDFs de actas de evaluación + chat de Q&A sobre el análisis). Es la feature más antigua y más usada del sistema.
- `MPM.Modules.Alertas` — `SinonimosIaService` (expansión de keywords, implementado hoy mismo, 2026-07-06).

## Requirements

### Functional Requirements

- **FR-001**: Todas las llamadas a Gemini MUST autenticarse vía ADC (identidad de la cuenta de desarrollo en local, identidad de la Service Account del servicio/Job en Cloud Run) — no vía API key en texto plano en `.env`/Secret Manager.
- **FR-002**: Las llamadas MUST usar el endpoint de Vertex AI (`{region}-aiplatform.googleapis.com`), no `generativelanguage.googleapis.com` — la Developer API no soporta bien la autenticación por cuenta (scope insuficiente con token estándar de usuario).
- **FR-003**: El análisis de PDFs escaneados (imagen) MUST seguir funcionando — el bug ya resuelto anteriormente (`GeminiService.AnalyzePdfViaFileApiAsync`, ver memoria `feedback-scraper-bugs` Bug 3) usaba la File API de la Developer API, que **no existe en Vertex AI**. Se reemplaza por referencia directa a GCS (`fileData.fileUri = gs://...`) cuando el storage provider es `gcs`; con storage `local` se mantiene el fallback `inlineData` (base64) que ya existía.
- **FR-004**: El sistema MUST seguir funcionando en desarrollo local (Docker Compose) sin que cada desarrollador tenga que generar y guardar una API key — usa las credenciales ADC de su propia cuenta (`gcloud auth application-default login`, corrido una vez, montado en el contenedor).
- **FR-005**: En producción (Cloud Run, `002-fase5-deploy-gcp`), las Service Accounts `mpm-api-sa`/`mpm-jobs-sa` MUST tener el rol `roles/aiplatform.user` — se agrega a la solicitud pendiente a Nicolás.
- **FR-006**: `GEMINI_API_KEY`/`Gemini:ApiKey` se eliminan del código y de la configuración — no queda un "modo con API key" en paralelo (Constitución: no backward-compat shims sin necesidad real).

### Non-Functional

- El formato JSON de request/response de Vertex AI usa `camelCase` (`generationConfig`, `systemInstruction`, `inlineData`, `fileData`) — distinto de lo que aceptaba la Developer API con algunos campos en `snake_case`. Se corrige en el código, no es opcional.
- El comportamiento observable para el usuario final (resultado del análisis, chat, sinónimos de alertas) NO debe cambiar — es un cambio de transporte/autenticación, no de funcionalidad.

## Success Criteria

- **SC-001**: Crear una alerta con keyword nueva genera sinónimos reales (no `null`) usando ADC, sin ninguna API key configurada en `.env`.
- **SC-002**: Un análisis de PDF (escaneado o no) sigue completándose correctamente vía Vertex AI.
- **SC-003**: `grep -r "GEMINI_API_KEY\|Gemini:ApiKey" src/` no devuelve resultados en código de producción tras la migración.
- **SC-004**: La solicitud a Nicolás (`002-fase5-deploy-gcp/solicitud-recursos-cloud-run.md`) incluye `roles/aiplatform.user`.

## Assumptions

- El proyecto GCP sigue siendo `tivit-cu010`, ya usado para GCS y ahora confirmado con `aiplatform.googleapis.com` y `generativelanguage.googleapis.com` habilitadas.
- La región para Vertex AI es `us-central1` (misma que el resto de la infraestructura de `002-fase5-deploy-gcp`).
- No se necesita una librería/SDK pesada de Vertex AI (`Google.Cloud.AIPlatform.V1`) — se mantiene el patrón `HttpClient` directo ya usado (`GeminiService`, `SinonimosIaService`), solo se cambia el endpoint, el esquema del body, y el mecanismo de auth (Bearer ADC en vez de `?key=`). Se agrega únicamente `Google.Apis.Auth` (liviano) para obtener el token ADC.
