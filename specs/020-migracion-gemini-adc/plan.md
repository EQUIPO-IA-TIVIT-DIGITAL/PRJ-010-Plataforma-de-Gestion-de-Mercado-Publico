# Implementation Plan: Migración de Gemini de API Key a ADC/Vertex AI

**Branch**: `020-migracion-gemini-adc` | **Status**: EN IMPLEMENTACIÓN
**Spec**: [spec.md](./spec.md)
**Actualizado**: 2026-07-06

---

## Summary

Reemplazar la autenticación de Gemini (API key en `.env`/Secret Manager) por **ADC (Application Default Credentials) sobre Vertex AI**, usando la identidad de la cuenta de desarrollo (local) o de la Service Account (Cloud Run) en vez de un secreto de texto plano. Afecta `GeminiService` (Análisis) y `SinonimosIaService` (Alertas).

---

## Investigación previa (hecha en vivo, 2026-07-06)

1. **`generativelanguage.googleapis.com` con la key actual de `.env`** → `401 ACCESS_TOKEN_TYPE_UNSUPPORTED`. La key no es una API key de Gemini (formato incorrecto: 53 chars, prefijo `AQ.`, no `AIzaSy...` de 39 chars).
2. **`generativelanguage.googleapis.com` con token ADC** (`gcloud auth print-access-token`) → `403 ACCESS_TOKEN_SCOPE_INSUFFICIENT`. La Developer API no acepta el scope estándar de un token de usuario para este método.
3. **Vertex AI (`us-central1-aiplatform.googleapis.com`) con el mismo token ADC** → `200 OK`, respuesta real de `gemini-2.5-flash`. **Esta es la ruta que se implementa.**

---

## Technical Context

**Endpoint nuevo**: `https://{region}-aiplatform.googleapis.com/v1/projects/{project}/locations/{region}/publishers/google/models/{model}:generateContent`
**Auth**: `Authorization: Bearer {token ADC}` — sin `?key=`.
**Formato de body**: camelCase (`generationConfig`, `systemInstruction`, `inlineData`, `fileData`) — Vertex AI no es tolerante con `snake_case` como sí lo era (parcialmente) la Developer API.
**Archivos grandes/escaneados**: Vertex AI no tiene la "File API" efímera de la Developer API. Se reemplaza por `fileData.fileUri = "gs://bucket/..."` cuando `Storage:Provider=gcs` (ya es el caso en producción). Con `Storage:Provider=local` se mantiene `inlineData` (base64) — mismo comportamiento que el fallback que ya existía, ahora es el único camino para storage local.
**Obtención del token**: paquete `Google.Apis.Auth`, clase `GoogleCredential.GetApplicationDefaultAsync()` con scope `https://www.googleapis.com/auth/cloud-platform`, cacheado/refrescado automáticamente por la librería (no hay que reinventar el refresh).
**Dónde vive el token provider**: `MPM.Shared` — lo consumen tanto `MPM.Modules.Analisis` como `MPM.Modules.Alertas` sin que se referencien entre sí (Principio I).

---

## Module Structure

```text
src/MPM.Shared/
└── Services/
    └── GoogleAdcTokenProvider.cs         ← NUEVO: obtiene y cachea el token ADC

src/MPM.Modules.Analisis/Services/
└── GeminiService.cs                      ← REESCRITO: Vertex AI, sin parámetro apiKey,
                                              fileData.fileUri para GCS, inlineData para local

src/MPM.Modules.Analisis/Controllers/AnalisisController.cs   ← quita GeminiApiKey/lectura de config
src/MPM.Modules.Analisis/Services/AnalisisService.cs          ← quita parámetro geminiApiKey
src/MPM.Modules.Analisis/Services/AnalisisBackgroundService.cs ← quita parámetro geminiApiKey

src/MPM.Modules.Alertas/Services/SinonimosIaService.cs  ← REESCRITO: Vertex AI, ADC

src/MPM.Api/appsettings.json / docker-compose.yml       ← quita Gemini:ApiKey/GEMINI_API_KEY,
                                                            agrega Vertex:Region
docker-compose.yml                                       ← monta credenciales ADC del host para
                                                            desarrollo local
specs/002-fase5-deploy-gcp/solicitud-recursos-cloud-run.md ← agrega roles/aiplatform.user
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | `GoogleAdcTokenProvider` vive en `MPM.Shared` (permitido para ambos módulos); Analisis y Alertas no se referencian entre sí |
| **II-IV** | ✅ N/A | Sin cambios de BD ni de multi-tenancy |
| **Sin backward-compat shims** | ✅ Aplicado | Se elimina `GEMINI_API_KEY`/`Gemini:ApiKey` por completo, no se deja como fallback |

---

## Riesgo aceptado / no cubierto en esta pasada

- No se prueba el flujo completo de análisis de PDF (requiere subir un documento real vía el frontend de Análisis) — se valida `SinonimosIaService` (Alertas) end-to-end porque ya se armó ese flujo de prueba hoy, y se revisa `GeminiService` por code review + compilación, no por ejecución completa del pipeline de Análisis.
- El mapeo de modelos (`gemini-2.5-pro`, `gemini-2.5-flash`) se asume disponible en Vertex AI en `us-central1` sin cuota especial — no verificado exhaustivamente más allá de la llamada de prueba exitosa con `gemini-2.5-flash`.
