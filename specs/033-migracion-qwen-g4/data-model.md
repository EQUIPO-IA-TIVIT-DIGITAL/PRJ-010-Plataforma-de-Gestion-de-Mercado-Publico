# Data Model: Migración Gemini → Qwen

**Spec**: `specs/033-migracion-qwen-g4/spec.md` | **Date**: 2026-08-11

## Decisión principal

Las tablas de análisis **no cambian**: `modelo_usado` (varchar) ya persiste el modelo real y soporta cualquier id nuevo (ej. `qwen3.7-g4`).

Se agrega **una sola tabla nueva** para la configuración del proveedor activo (requisito del switch del super admin, US4). Acceso exclusivo vía stored procedures (constitución II).

## Nueva entidad: `system_ai_provider`

Configuración global del proveedor de IA. **No es multi-tenant** (es infraestructura del sistema, no dato de negocio) → no lleva `tenant_id`. Una sola fila activa.

| Columna | Tipo | Descripción |
|---------|------|-------------|
| `id` | int identity PK | |
| `provider` | varchar(20) not null | `gemini` o `openai` (activo) |
| `endpoint` | varchar(500) null | Base URL del proveedor `openai` (URL entregada por el equipo); null para `gemini` |
| `model` | varchar(100) not null | Id del modelo activo (ej. `gemini-2.5-pro`, `qwen3.7-g4`) — es el valor que se persiste en `modelo_usado` |
| `updated_by_user_id` | uuid not null | Quién cambió el proveedor (auditoría FR-014) |
| `updated_by_username` | varchar(150) not null | Email/username del que cambió |
| `updated_at` | timestamptz not null default now() | Cuándo cambió |
| `record_status` | char(1) not null default 'A' | Soft delete (convención de auditoría del proyecto) |

**Restricción**: una sola fila `record_status = 'A'` (unique index parcial).

**Migración**: `V130__Add_System_AI_Provider.sql` (embebida en `src/MPM.Api/Database/Scripts/`, mecanismo `DatabaseInitializer` — constitución III).

## Stored procedures nuevos

| SP | Propósito |
|----|-----------|
| `usp_SystemConfig_ObtenerAiProvider` | Devuelve la fila activa (o nada si no existe) |
| `usp_SystemConfig_ActualizarAiProvider` | UPSERT atómico de provider/endpoint/model + auditoría (último cambio gana) |

Ambos reciben/usan `DbConnectionFactory` (MPM.Core) — sin ORM, Dapper con `MatchNamesWithUnderscores` (constitución II).

## Precedencia de resolución del proveedor (runtime)

```
1. system_ai_provider (fila activa)   ← fuente de verdad (persiste entre reinicios)
2. AI:Provider / AI:Endpoint / AI:Model / AI:ApiKey (env)  ← fallback/bootstrapping
3. default: gemini / gemini-2.5-pro
```

La fila se crea la primera vez que el super admin cambia el switch (seed desde env). Si la tabla está vacía o hay error de BD al resolver → env (FR-017).

## Entidades existentes referenciadas

### Análisis (`modelo_usado`)

- Ya persiste el modelo real; puntos de escritura verificados: `AnalisisService.cs:151`, `AnalisisBackgroundService.cs:180` (hoy leen `GeminiService.ModelName` → pasan a leer el modelo resuelto del proveedor activo).
- Sin cambios de columna, tipo ni longitud → sin migración.

### Usuarios (`usuarios`)

- Referenciada por la auditoría del switch (`updated_by_user_id` → `usuarios.id`). Sin cambios.
- El seed `admin@tivit.cl` con rol `SuperAdmin` ya existe (V042) — no se siembra nada nuevo.

## Validaciones

- [ ] No se tocan tablas de análisis ni sus SPs.
- [ ] La nueva tabla y SPs siguen convenciones del proyecto (nombres, audit, soft delete).
- [ ] El `search_vector` de búsqueda semántica no se toca.
