# Contrato: API de administración del proveedor IA (switch super admin)

**Spec**: `specs/033-migracion-qwen-g4/spec.md` | **Date**: 2026-08-11
**Ubicación propuesta**: `src/MPM.Api/Controllers/SystemConfigController.cs` (patrón `HealthController`) + handler/servicio en `MPM.Core/Data` + `MPM.Core/SystemConfig` (transversal).

## Autorización

- Ambos endpoints requieren JWT con rol **`SuperAdmin`** (policy por rol, no chequeo de email).
- Sin el rol: `403 Forbidden`; el frontend además oculta la entrada de administración (FR-016).

## GET /api/system/ai-provider

Devuelve el proveedor activo y su estado. Sin body.

**200 OK**:

```jsonc
{
  "provider": "openai",                // "gemini" | "openai" (resuelto con precedencia BD > env > default)
  "model": "qwen3.7-g4",
  "endpoint": "https://qwen.tivit.internal/v1",   // null si provider = gemini
  "resolvedFrom": "database",          // "database" | "environment" | "default" (diagnóstico)
  "lastChange": {
    "provider": "gemini",              // valor anterior
    "model": "gemini-2.5-pro",
    "updatedBy": "admin@tivit.cl",
    "updatedAt": "2026-08-11T10:00:00Z"
  }                                    // null si nunca se cambió (tabla vacía)
}
```

## PUT /api/system/ai-provider

Cambia el proveedor activo. Solo super admin. **UPSERT atómico** (el último cambio gana).

**Request**:

```jsonc
{
  "provider": "openai",        // "gemini" | "openai"
  "endpoint": "https://qwen.tivit.internal/v1",   // requerido si provider=openai; null/omitido si gemini
  "model": "qwen3.7-g4"        // requerido; ej. "gemini-2.5-pro" si provider=gemini
}
```

**Validaciones**:

| Regla | Respuesta |
|-------|-----------|
| `provider` no es `gemini`/`openai` | 400 `{ "code": "INVALID_PROVIDER", ... }` |
| `provider=openai` sin `endpoint` válido (http/https, no vacío) | 400 `{ "code": "INVALID_ENDPOINT", ... }` |
| `model` vacío | 400 `{ "code": "INVALID_MODEL", ... }` |
| Sin rol SuperAdmin | 403 (policy) |
| Error de BD al persistir | 500 con código de error del catálogo MPM |

**200 OK**: mismo body de GET (con `resolvedFrom: "database"` y `lastChange` actualizado).

**Efecto**: invalidación de la cache del `LlmClientResolver` → el análisis siguiente usa el nuevo proveedor (SC-002). No reinicia nada.

## Errores (formato MPM existente)

Misma envoltura de errores del sistema (`ErrorHandlingMiddleware` de MPM.Core): `{ "error": { "code", "message", ... } }`. Códigos nuevos propuestos:

| Código | Caso |
|--------|------|
| `INVALID_PROVIDER` | provider desconocido en PUT |
| `INVALID_ENDPOINT` | provider=openai sin endpoint válido |
| `INVALID_MODEL` | model vacío |

## Notas de implementación

- El handler usa `DbConnectionFactory` + SPs `usp_SystemConfig_ObtenerAiProvider` / `usp_SystemConfig_ActualizarAiProvider` (constitución II).
- `updated_by_user_id`/`updated_by_username` se toman del `TenantContext` (constitución IV: los controllers no leen claims directo).
- La UI (página `/admin/ia`) usa este contrato para: mostrar estado (`useQuery`) y cambiar (`useMutation` con `Modal.confirm`).
