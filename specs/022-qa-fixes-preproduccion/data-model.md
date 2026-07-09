# Phase 1: Data Model — Corrección de Hallazgos QA Pre-Producción

La mayoría de las correcciones de este feature son de comportamiento (código C#/Node) y no tocan el modelo de datos. Solo dos hallazgos requieren cambios de esquema.

## Entidad nueva: `auth_eventos` (BUG-010)

Registra cada inicio de sesión exitoso para medir adopción.

| Columna | Tipo | Notas |
|---|---|---|
| `id` | `BIGSERIAL PRIMARY KEY` | |
| `user_id` | `VARCHAR(50) NOT NULL` | Coincide con el tipo de `user_id` usado en `TenantContext` / claims JWT |
| `tenant_id` | `VARCHAR(50) NOT NULL` | Del `TenantContext` |
| `email` | `VARCHAR(200) NOT NULL` | Denormalizado a propósito: si el usuario cambia de email después, el evento histórico conserva el valor con el que inició sesión ese día |
| `ip_address` | `VARCHAR(45) NULL` | IPv4/IPv6; nullable porque no siempre está disponible (proxies, tests) |
| `user_agent` | `TEXT NULL` | Nullable, informativo |
| `created_at` | `TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP` | |

**Índices**: `idx_auth_eventos_user_id_created_at` sobre `(user_id, created_at DESC)` — soporta la consulta típica "últimos logins de un usuario"; `idx_auth_eventos_created_at` sobre `(created_at)` — soporta reportes de adopción por rango de fechas.

**Stored procedure**: `usp_Auth_RegistrarEvento(p_user_id, p_tenant_id, p_email, p_ip_address, p_user_agent, OUT p_error_msg)` — `INSERT` simple, sin validaciones de negocio más allá de `user_id`/`tenant_id`/`email` no nulos. No se agrega un `usp_*_Listar` en este feature (fuera de alcance: la consulta de adopción para negocio se resuelve con SQL directo o un endpoint de reporte si se pide explícitamente después; el requisito de `spec.md` es que el registro *exista y sea consultable*, no que tenga una pantalla dedicada).

**Relaciones**: Ninguna FK formal a `usuarios` — mismo patrón denormalizado que otras tablas de auditoría del proyecto (evita acoplar el registro histórico al ciclo de vida del usuario).

**Migración**: `V092__Create_Auth_Eventos.sql`.

## Cambio de comportamiento (sin nueva tabla): `analisis_workspaces.estado` (BUG-002)

No se agrega columna ni tabla. Se documenta el ciclo de vida ya existente de `estado` porque `AnalisisRecoveryWorker` (ver `research.md` R2) depende de él:

```
pendiente → analizando → completado
                       ↘ error
```

- `pendiente`: estado inicial de un workspace recién creado, antes de subir un documento.
- `analizando`: seteado síncronamente por `AnalisisService.cs:99` justo antes de encolar el análisis. **Es la señal durable que el `AnalisisRecoveryWorker` usará para detectar trabajo huérfano.**
- `completado` / `error`: estados terminales, seteados al final de `ProcessAnalisisAsync`.

**Regla de recuperación**: un workspace en `analizando` sin fila correspondiente en `analisis_resultados` (vía `usp_AnalisisResultados_ObtenerPorWorkspace`) y con `updated_at` más antiguo que `Analisis:RecoveryThresholdMinutes` (config, default 5) se considera huérfano y se reprocesa.

## Modificación de stored procedure: `usp_Licitaciones_Listar` (BUG-008)

No cambia la forma de la tabla `licitaciones` (la columna `search_vector` ya existe desde V066). Cambia únicamente la lógica de filtrado dentro del proc:

- Antes: `l.nombre ILIKE '%' || p_search || '%' OR l.codigo_externo ILIKE '%' || p_search || '%'`
- Después: `l.search_vector @@ websearch_to_tsquery('spanish', p_search) OR l.codigo_externo ILIKE '%' || p_search || '%'` (código externo se mantiene con `ILIKE` porque no es texto libre — es un identificador corto; se agrega índice trigram para que ese `ILIKE` sea eficiente también).

**Índice nuevo**: `CREATE INDEX idx_licitaciones_codigo_externo_trgm ON licitaciones USING gin (codigo_externo gin_trgm_ops);` (requiere `CREATE EXTENSION IF NOT EXISTS pg_trgm;` si no está ya habilitada — verificar en Phase 2/implementación).

**Migración**: `V093__Fix_usp_Licitaciones_Listar_Search.sql`.

## Sin cambios de datos

BUG-001, BUG-003, BUG-004, BUG-005, BUG-006, BUG-007, BUG-009, BUG-011, BUG-012, BUG-013 son correcciones de comportamiento (lógica de arranque, gates de configuración, manejo de procesos, seguridad de validación, orden de consultas, escape de texto) que no requieren cambios de esquema.
