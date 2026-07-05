# Implementation Plan: Fase 2 — Automatización del Pipeline de Scraping y Análisis IA

**Branch**: `main` | **Date**: 2026-06-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification Fase 2 — Automatización del scraping de "actas de evaluación" y pipeline completo con Gemini AI.

---

## Summary

El pipeline de automatización está ~90% implementado. Los componentes clave (`agente-mp.js`, `pipelineAnalisisCompleto`, `AnalisisBackgroundService`) existen y funcionan. El único bloqueante es que el **Dockerfile del API no tiene Node.js instalado**, lo que impide que el `ScraperBackgroundService` ejecute el scraper. Adicionalmente, faltan variables de entorno críticas en `docker-compose.yml`.

**Approach**: Actualizar el Dockerfile del API para incluir Node.js + Playwright, copiar el directorio `tools/`, agregar un parámetro configurable para el path del script, y añadir las variables faltantes al docker-compose.

---

## Technical Context

**Language/Version**: .NET 8 (C#) + Node.js 20 LTS

**Primary Dependencies**: Playwright (Node.js), pg (PostgreSQL client Node.js), Dapper (.NET), ASP.NET Core

**Storage**: PostgreSQL (`licitaciones_adjuntos`, `scraper_sync_log`, `analisis_*`) + Local `/app/uploads` (PDFs descargados; producción → GCS)

**Testing**: Validación manual vía docker compose; E2E: login en Mercado Público → workspace creado → análisis completado en frontend

**Target Platform**: Linux Docker container (dev) / Cloud Run GCP (producción futura)

**Performance Goals**: Primera corrida histórica (2025) puede tomar 1–4 horas; corridas incrementales < 15 min

**Constraints**: `MP_RUT`/`MP_PASSWORD` son secretos no commiteables; Playwright browsers ~300MB de espacio extra en imagen

**Scale/Scope**: ~10–50 licitaciones adjudicadas por mes; análisis Gemini ~10–30s por acta

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | El scraper JS es una herramienta externa que llama a la API; no es un módulo .NET |
| **II. Stored Procedures First** | ✅ Sin violación | `usp_Licitacion_UpsertFromScraper`, `usp_Licitaciones_Adjuntos_Upsert`, `usp_ScraperSync_*` ya existen |
| **III. Migraciones SQL** | ✅ Sin violación | No se necesitan nuevas migraciones; V062/V063 tienen todas las tablas/SPs |
| **IV. Multi-Tenancy** | ✅ N/A | El scraper corre como servicio de sistema (userId fijo `000...0`) |
| **V. Abstracción de Storage** | ✅ Sin violación | `AnalisisService.SubirDocumentoAsync` usa `IStorageService` |
| **VII. Testing** | ⚠️ Sin unit tests del scraper | El scraper JS no tiene tests automatizados. Aceptable para herramienta operativa; E2E validado manualmente según [quickstart.md](./quickstart.md). |

---

## Project Structure

### Documentation (this feature)
```text
specs/001-analisis-fases-sdd/
├── spec.md                       ✅
├── plan.md                       ✅ (este archivo)
├── research.md                   ✅
├── data-model.md                 ✅
├── quickstart.md                 ✅
├── contracts/
│   └── scraper-pipeline.md       ✅
└── tasks.md                      (pendiente → /speckit-tasks)
```

### Source Code (archivos a modificar)
```text
src/MPM.Api/Dockerfile
  → Añadir Node.js 20 + instalar Playwright Chromium + copiar tools/

docker-compose.yml
  → Añadir variables de entorno del scraper al servicio `api`

src/MPM.Modules.Licitaciones/Services/ScraperBackgroundService.cs
  → Hacer configurable el path del script (env var Scraper:ScriptPath)
  → Pasar credenciales MP al ProcessStartInfo del proceso Node.js

tools/scraper-mp/package.json          (verificar existencia)
  → Debe tener playwright, dotenv, pg como dependencias

.env.example                           (crear o actualizar)
  → Documentar MP_RUT, MP_PASSWORD, SCRAPER_ENABLED, MP_ANALISIS_IA
```

---

## Tareas de Implementación

### T1: Actualizar `src/MPM.Api/Dockerfile` — Añadir Node.js + Playwright

**Archivo**: `src/MPM.Api/Dockerfile`

Convertir en multi-stage build. El stage de runtime final debe:
1. Instalar Node.js 20 (vía apt o copiando desde imagen `node:20-slim`)
2. Instalar dependencias del scraper (`npm ci`)
3. Instalar Playwright Chromium con sus dependencias del SO (`playwright install chromium --with-deps`)
4. Copiar `tools/scraper-mp/` a `/app/tools/`

Punto clave: la imagen base `mcr.microsoft.com/dotnet/aspnet:8.0` es Debian-based, compatible con la instalación de Node.js via `apt` o `nodesource`.

### T2: Actualizar `ScraperBackgroundService.cs` — Path y vars configurables

**Archivo**: `src/MPM.Modules.Licitaciones/Services/ScraperBackgroundService.cs`

Cambios:
- Leer el path del script desde `config["Scraper:ScriptPath"]` o `config["SCRAPER_SCRIPT_PATH"]` (fallback al path relativo actual para desarrollo local)
- En `EjecutarScraperAsync`, agregar al `ProcessStartInfo.EnvironmentVariables`: `MP_RUT`, `MP_PASSWORD`, `MP_ANALISIS_IA`, `API_BASE_URL`, `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`, `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`, `MP_HEADLESS`

### T3: Actualizar `docker-compose.yml` — Variables de entorno faltantes

**Archivo**: `docker-compose.yml`

En el servicio `api`, agregar debajo de las variables existentes:

```yaml
- MP_RUT=${MP_RUT:-}
- MP_PASSWORD=${MP_PASSWORD:-}
- SCRAPER_ENABLED=${SCRAPER_ENABLED:-false}
- Scraper__Enabled=${SCRAPER_ENABLED:-false}
- Scraper__IntervalHours=${SCRAPER_INTERVAL_HOURS:-12}
- Scraper__ScriptPath=/app/tools/agente-mp.js
- MP_ANALISIS_IA=${MP_ANALISIS_IA:-true}
- MP_HEADLESS=true
- MP_FECHA_DESDE=${MP_FECHA_DESDE:-01-01-2025}
- API_BASE_URL=http://localhost:80
```

### T4: Verificar y crear `tools/scraper-mp/package.json`

Verificar que existe con las dependencias requeridas. Si no existe o está incompleto:
```json
{
  "name": "mpm-scraper",
  "version": "1.0.0",
  "type": "module",
  "dependencies": {
    "playwright": "^1.44.0",
    "dotenv": "^16.4.0",
    "pg": "^8.11.0"
  }
}
```

### T5: Crear/actualizar `.env.example`

Documentar todas las variables nuevas del scraper para onboarding de nuevos devs.

---

## Complexity Tracking

| Item | Por qué necesario | Alternativa más simple rechazada |
|---|---|---|
| Multi-stage Dockerfile con Node.js | Playwright requiere browsers (~300MB) y dependencias del SO; no se puede instalar rápido en runtime | Contenedor scraper separado — implica más cambios en docker-compose y pérdida de integración con NotificacionesService |
| Path configurable del script | El path calculado con `AppContext.BaseDirectory` no funciona en Docker | Hardcodear `/app/tools/agente-mp.js` — rompería el desarrollo local |

---

## Verification

Seguir [quickstart.md](./quickstart.md):

1. `docker compose up --build -d` — todos los contenedores healthy
2. `docker compose logs api | grep -i scraper` → `ScraperBackgroundService starting`
3. Ejecutar scraper manualmente una vez con `SCRAPER_ENABLED` activado o ejecutando el script directamente
4. Verificar en BD: `SELECT * FROM licitaciones_adjuntos WHERE tipo='acta_evaluacion' LIMIT 5;`
5. Verificar en frontend: workspace creado → dashboard con análisis Gemini visible

---

---

# Implementation Plan: Fase 4 — Notificaciones y Seguimiento Activo (US3)

**Branch**: `main` | **Date**: 2026-06-24 | **Spec**: [spec.md](./spec.md)

**Research**: [research-fase4.md](./research-fase4.md)
**Data model**: [data-model-fase4.md](./data-model-fase4.md)
**Contract**: [contracts/seguimiento-aclaraciones.md](./contracts/seguimiento-aclaraciones.md)
**Quickstart**: [quickstart-fase4.md](./quickstart-fase4.md)

---

## Summary

El módulo de Notificaciones está completo (tabla, SPs, service, controller, frontend bell + page). Lo que falta para US3 es:

1. **Dos tablas nuevas**: `licitaciones_seguidas` y `licitaciones_aclaraciones`
2. **Cinco SPs nuevos**: toggle seguir, check seguida, obtener para monitor, upsert aclaración, listar seguidas del usuario
3. **Extensión del modelo `ApiMpLicitacion`**: campo `Preguntas.Listado[]` para leer aclaraciones de la MP API
4. **Nuevo `AclaracionMonitorService`**: background service (cada 30 min) que detecta nuevas aclaraciones y notifica a los seguidores
5. **Tres endpoints nuevos**: POST seguir, GET seguida, GET seguidas
6. **Frontend**: botón estrella en la lista de licitaciones + rendering especial para notificaciones tipo `aclaracion_detectada`

---

## Technical Context

**Language/Version**: .NET 8 (C#) + React 18 (TypeScript)

**Primary Dependencies**: Dapper, ASP.NET Core BackgroundService, MP API REST (polling)

**Storage**: PostgreSQL — 2 tablas nuevas + 5 SPs nuevos. Sin nuevas dependencias externas.

**Testing**: Validación manual vía docker compose. Ver [quickstart-fase4.md](./quickstart-fase4.md) Escenarios 1–4.

**Target Platform**: Docker (dev) / Cloud Run GCP (prod)

**Performance Goals**: Ciclo de monitor ≤ 1 min para 50 licitaciones seguidas (1 req/s a MP API)

**Constraints**:
- MP API no tiene webhooks → solo polling
- Rate limit de MP API: 1 req/s estimado conservador
- `MP_TICKET` ya configurado — se reutiliza

**Scale/Scope**: ~5–50 licitaciones seguidas activas; ~0–10 aclaraciones nuevas por ciclo

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | `AclaracionMonitorService` vive en `MPM.Modules.Licitaciones`; inyecta `NotificacionesService` vía DI |
| **II. Stored Procedures First** | ✅ Sin violación | Todo acceso a DB via `usp_Licitaciones_*` |
| **III. Migraciones SQL** | ✅ Sin violación | V072 (tablas) + V073 (SPs); nombradas correctamente |
| **IV. Multi-Tenancy** | ✅ Sin violación | `usuario_id` en `licitaciones_seguidas`; notificaciones ya son por usuario |
| **V. Abstracción de Storage** | ✅ N/A | No se almacenan archivos en esta fase |
| **VII. Testing** | ⚠️ Sin unit tests | Igual que fases anteriores; validación E2E manual según quickstart |

---

## Project Structure

### Nuevos archivos
```text
src/MPM.Api/Database/Scripts/
├── V072__Create_licitaciones_seguidas_aclaraciones.sql
└── V073__SP_Seguimiento_Aclaraciones.sql

src/MPM.Modules.Licitaciones/
├── Services/AclaracionMonitorService.cs          (nuevo)
├── Data/SeguimientoHandler.cs                    (nuevo)
└── Controllers/LicitacionesController.cs         (extender)

src/mpm-web/src/
├── hooks/useLicitaciones.ts                      (extender)
└── pages/LicitacionesPage.tsx                    (extender)
```

### Archivos modificados
```text
src/MPM.Modules.Licitaciones/Services/ApiMpService.cs
  → Agregar ApiMpPreguntas + ApiMpAclaracion al modelo

src/MPM.Modules.Licitaciones/Data/LicitacionStoredProcedures.cs
  → Agregar 5 constantes de SPs nuevos

src/MPM.Modules.Licitaciones/ModuleRegistration.cs
  → Registrar AclaracionMonitorService como hosted service

src/MPM.Api/Program.cs o appsettings.json
  → MONITOR_ENABLED, MONITOR_INTERVAL_MINUTES
```

---

## Tareas de Implementación

### T1: Migración V072 — Tablas `licitaciones_seguidas` y `licitaciones_aclaraciones`

Crear `src/MPM.Api/Database/Scripts/V072__Create_licitaciones_seguidas_aclaraciones.sql`.
Esquema completo en [data-model-fase4.md](./data-model-fase4.md) sección "Nuevas entidades".

### T2: Migración V073 — Stored Procedures de seguimiento

Crear `src/MPM.Api/Database/Scripts/V073__SP_Seguimiento_Aclaraciones.sql`.
Los 5 SPs detallados en [data-model-fase4.md](./data-model-fase4.md) sección "SPs nuevos".

### T3: Extender `ApiMpLicitacion` con campo `Preguntas`

En `src/MPM.Modules.Licitaciones/Services/ApiMpService.cs`:
- Agregar `ApiMpPreguntas` y `ApiMpAclaracion` como clases públicas
- Agregar `[JsonPropertyName("Preguntas")] public ApiMpPreguntas? Preguntas { get; set; }` a `ApiMpLicitacion`
- Detalle del modelo en [research-fase4.md](./research-fase4.md) sección 4

### T4: Crear `SeguimientoHandler.cs`

En `src/MPM.Modules.Licitaciones/Data/SeguimientoHandler.cs`:
- `SeguirToggleAsync(usuarioId, licitacionId)` → `(string Accion, string? Error)`
- `EsSeguidaAsync(usuarioId, licitacionId)` → `bool`
- `ObtenerParaMonitorAsync(estados int[])` → `IEnumerable<LicitacionParaMonitorDto>`
- `AclaracionUpsertAsync(...)` → `(bool EsNueva, long Id)`
- `ObtenerSeguidasAsync(usuarioId)` → `IEnumerable<LicitacionSeguidaDto>`

### T5: Crear `AclaracionMonitorService.cs`

En `src/MPM.Modules.Licitaciones/Services/AclaracionMonitorService.cs`:
- Hereda de `BackgroundService`
- Lee `MONITOR_ENABLED` y `MONITOR_INTERVAL_MINUTES` desde `IConfiguration`
- Ciclo: llama `SeguimientoHandler.ObtenerParaMonitorAsync([1,2,4])`
- Por cada licitación: `ApiMpService.GetDetalleAsync()` → itera `Preguntas.Listado`
- Por cada aclaración: `SeguimientoHandler.AclaracionUpsertAsync()` → si nueva: `NotificacionesService.CrearAsync("aclaracion_detectada", ...)`
- Delay de 1s entre requests a MP API
- Log: nivel INFO para ciclos, DEBUG para licitaciones individuales

### T6: Agregar endpoints de seguimiento a `LicitacionesController`

En `src/MPM.Modules.Licitaciones/Controllers/LicitacionesController.cs`:
- `POST /api/v1/licitaciones/{id}/seguir` — toggle, retorna `accion`
- `GET /api/v1/licitaciones/{id}/seguida` — retorna `esSeguida`
- `GET /api/v1/licitaciones/seguidas` — lista de seguidas del usuario

Detalle de contratos en [contracts/seguimiento-aclaraciones.md](./contracts/seguimiento-aclaraciones.md)

### T7: Registrar el servicio en `ModuleRegistration.cs`

En `src/MPM.Modules.Licitaciones/ModuleRegistration.cs`:
- `services.AddHostedService<AclaracionMonitorService>()`
- `services.AddScoped<SeguimientoHandler>()`

### T8: Frontend — botón "Seguir" en `LicitacionesPage.tsx`

- Agregar hook `useSeguirToggle(id)` y `useEsSeguida(id)` en `useLicitaciones.ts`
- En `LicitacionesPage.tsx`: columna extra con `<Button icon={<StarOutlined />}` que toggle la estrella
- Color amarillo si `esSeguida = true`, gris si false
- `useMutation` para POST al API; invalida query `esSeguida` al completar

### T9: Frontend — rendering especial de notificaciones de aclaración

En `NotificacionesPage.tsx` (verificar si ya renderiza metadata):
- Para tipo `aclaracion_detectada`: mostrar `<QuestionCircleOutlined />` + link al `codigo_externo`
- En `NotificationBell`: el badge ya funciona — verificar que el count actualiza correctamente

### T10: Agregar variables de entorno

En `.env.example`:
```
MONITOR_ENABLED=true
MONITOR_INTERVAL_MINUTES=30
```

En `docker-compose.yml` servicio `api`:
```yaml
- MONITOR_ENABLED=${MONITOR_ENABLED:-true}
- MONITOR_INTERVAL_MINUTES=${MONITOR_INTERVAL_MINUTES:-30}
```

---

## Complexity Tracking

| Item | Por qué necesario | Alternativa más simple rechazada |
|---|---|---|
| Tabla `licitaciones_aclaraciones` | Sin ella, no hay idempotencia: el monitor notificaría la misma aclaración cada 30 min | Guardar `notificado=true` directamente en `notificaciones` — pero no permite rastrear cuáles aclaraciones ya se procesaron |
| SP `usp_Licitaciones_ObtenerParaMonitor` con `usuario_ids[]` | Necesita agregar los seguidores por licitación para notificar a todos | Hacer N queries por licitación — ineficiente con muchos usuarios |
| `AclaracionMonitorService` separado de `SyncEngineService` | El sync corre 1x/día; las aclaraciones necesitan 30 min | Agregar lógica de aclaraciones al sync diario — no cumple SC-005 |

---

## Verification

Seguir [quickstart-fase4.md](./quickstart-fase4.md):

1. Marcar una licitación activa como seguida (Escenario 1)
2. Verificar en BD que `licitaciones_seguidas` tiene el registro
3. Verificar logs del monitor (Escenario 2)
4. Confirmar que la notificación llega al bell y a la página (Escenario 3)
5. Probar los endpoints directamente con curl (Escenario 4)
