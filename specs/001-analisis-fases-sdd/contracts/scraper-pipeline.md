# Contract: Scraper → MPM API Pipeline

**Feature**: MPM CU010 — Fase 2
**Date**: 2026-06-23

El scraper Node.js (`agente-mp.js`) llama a la MPM API como cliente de servicio.
Autenticación: JWT firmado con `JWT_SECRET` (rol `admin`, sub `00000000-...`, 1h TTL).

---

## POST /api/v1/analisis/workspaces

**Llamado por**: `api-client.js :: crearWorkspaceAnalisis(licitacionId, nombre)`

**Request:**
```json
{
  "licitacionId": 42,
  "nombre": "Licitación XYZ-2025-001"
}
```

**Response (201):**
```json
{
  "success": true,
  "data": { "id": 7, "estado": "pendiente", ... }
}
```

---

## POST /api/v1/analisis/workspaces/{id}/documentos

**Llamado por**: `api-client.js :: subirDocumento(workspaceId, filePath, fileName)`

**Request**: `multipart/form-data` con campo `archivo` (PDF ≤ 10MB)

**Response (201):**
```json
{
  "success": true,
  "data": { "id": 3, "nombreArchivo": "acta-evaluacion.pdf", ... }
}
```

---

## POST /api/v1/analisis/workspaces/{id}/analizar

**Llamado por**: `api-client.js :: iniciarAnalisis(workspaceId)`

**Request**: `{}` (usa el último documento subido)

**Response (200):**
```json
{
  "success": true,
  "data": { "id": 0, "estado": "analizando", ... }
}
```

**Nota**: El análisis es asíncrono en el backend (AnalisisBackgroundService). El endpoint responde inmediatamente con estado `analizando`.

---

## Env vars requeridas por el scraper (lado Node.js)

| Variable | Descripción | Ejemplo |
|---|---|---|
| `MP_RUT` | RUT de TIVIT en Mercado Público | `76123456-7` |
| `MP_PASSWORD` | Contraseña de Mercado Público | `***` |
| `MP_HEADLESS` | Ejecutar browser sin UI | `true` |
| `MP_ANALISIS_IA` | Activar pipeline IA | `true` |
| `MP_FECHA_DESDE` | Fecha inicio búsqueda | `01-01-2025` |
| `MP_DELAY_MS` | Delay entre acciones | `2000` |
| `API_BASE_URL` | URL de la MPM API | `http://api:80` |
| `JWT_SECRET` | Secreto JWT (mismo que la API) | `***` |
| `JWT_ISSUER` | Issuer del JWT | `TIVIT.MPM` |
| `JWT_AUDIENCE` | Audience del JWT | `MPM.Users` |
| `DB_HOST` | Host de PostgreSQL | `db` |
| `DB_PORT` | Puerto de PostgreSQL | `5432` |
| `DB_NAME` | Nombre de la base de datos | `mpm` |
| `DB_USER` | Usuario de PostgreSQL | `mpm` |
| `DB_PASSWORD` | Contraseña de PostgreSQL | `***` |
| `SCRAPER_INTERVAL_HOURS` | Intervalo de ejecución | `12` |
| `SCRAPER_DAEMON` | Modo daemon (auto-programado) | `true` |

## Env vars requeridas en la API (.NET) para el ScraperBackgroundService

| Variable | Descripción | Valor en Docker |
|---|---|---|
| `Scraper__Enabled` | Habilitar el service | `true` |
| `Scraper__ScriptPath` | Ruta al script JS | `/app/tools/agente-mp.js` |
| `Scraper__IntervalHours` | Intervalo de ejecución | `12` |
| `SCRAPER_ENABLED` | Alternativa flat | `true` |
