# API Contracts: Ajustes Urgentes del Cliente

**Feature**: 017-ajustes-urgentes-cliente | **Date**: 2026-07-01

Solo se listan contratos nuevos o modificados. Todo endpoint requiere JWT Bearer; el tenant/usuario sale del `TenantContext` (nunca de parámetros del cliente).

## 1. Notificaciones — NUEVOS

### DELETE `/api/v1/notificaciones/{id}`

Elimina una notificación del usuario autenticado.

- **Path**: `id` (long) — id de la notificación
- **200 OK**: `{ "success": true }`
- **404 Not Found**: la notificación no existe o no pertenece al usuario/tenant
- **401 Unauthorized**: token inválido/expirado

### DELETE `/api/v1/notificaciones`

Elimina todas las notificaciones del usuario autenticado.

- **200 OK**: `{ "success": true, "data": { "eliminadas": <int> } }`
- **401 Unauthorized**

## 2. Análisis — MODIFICADO (respuesta extendida, retrocompatible)

### GET `/api/v1/analisis/{id}/resultado` (endpoint existente)

El JSON de resultado agrega la sección opcional `validacion_documental` (ver [data-model.md](../data-model.md#2-resultado-de-análisis--extensión-del-json-columna-existente)). Los clientes existentes no se rompen: la sección puede estar ausente en análisis históricos.

### POST `/api/v1/analisis/{id}/chat` (endpoint existente — contrato de formato)

Sin cambios de firma. Se endurece la garantía de formato de `respuesta`: siempre Markdown válido, sin fences envolventes (```json / ```markdown), listas y tablas en sintaxis Markdown estándar. Esto es contrato de contenido, verificado por test de integración.

## 3. Licitaciones / Sync — SIN CAMBIOS DE FIRMA

- `POST /api/v1/licitaciones/sync` se **conserva** (uso operacional) aunque la UI elimina el botón.
- La sincronización automática pasa a cadencia semanal con backfill 2025-2026 (comportamiento interno del hosted service, sin contrato HTTP nuevo).
- `GET /api/v1/licitaciones` (listado + filtros) sin cambios; la UI de filtros se rediseña sobre los mismos parámetros existentes.

## 4. Auth — SIN CAMBIOS DE FIRMA

- `POST /api/v1/auth/login` sin cambios.
- El flujo forgot/reset password **se conserva en backend**; solo se elimina el enlace de acceso en la UI de login.
- Contrato de comportamiento frontend: toda respuesta 401 de cualquier endpoint dispara el flujo único de sesión expirada (limpieza + redirect a `/login` + aviso). Verificado por E2E.

## 5. UI Contracts (frontend)

| Ruta | Cambio |
|------|--------|
| `/login` | Sin enlace "¿Olvidaste tu contraseña?"; muestra aviso si `mpm_session_expired` está presente |
| `/analisis/:id/chat` | **NUEVA** — vista dedicada del chat contextual de la licitación analizada |
| `/analisis/:id/dashboard` | Agrega tarjeta "Comparativa de documentos" en el resumen + botón "Abrir chat en vista completa"; exportar PDF genera documento estructurado |
| `/licitaciones` | Un solo buscador, sin búsqueda inteligente ni botón sincronizar, con "Reiniciar filtros", layout compacto |
| `/notificaciones` | Acción eliminar por fila + "Borrar todas" con confirmación |
| `/catalogos` | Click en fila → Drawer con explicación del concepto |
| Sidebar (todas) | Avatar + nombre + rol ("admin TIVIT"), sin correo |
