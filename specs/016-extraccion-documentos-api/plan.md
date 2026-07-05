# Implementation Plan: Extracción de Documentos vía API Directa

**Branch**: `016-extraccion-documentos-api` | **Date**: 2026-07-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/016-extraccion-documentos-api/spec.md`

## Summary

Reemplazar la descarga de adjuntos (Acta de Evaluación, bases, anexos, resoluciones) que hoy hace el scraper Playwright (`tools/scraper-mp`) por un **servicio C# en `MPM.Modules.Licitaciones`** que descarga vía **HTTP directo**. La autenticación se resuelve con **sesión cacheada**: el login por Keycloak (que es la parte frágil) se hace una vez cada N horas reutilizando el login Node ya existente, y las cookies resultantes se cachean; toda la descarga de adjuntos por licitación es HTTP puro (sin navegador). Ante cualquier fallo del flujo directo para una licitación, se cae automáticamente al scraper Playwright actual (US2), y ambos flujos se comparan durante un período de validación en paralelo (US3) antes de retirar el navegador como mecanismo principal.

## Technical Context

**Language/Version**: C# / .NET 8 (backend). El scraper Node (`tools/scraper-mp`) se conserva como fallback.

**Primary Dependencies**: `HttpClient` con `CookieContainer` + `SocketsHttpHandler`; parser HTML **AngleSharp** (nueva dependencia, para leer la tabla `#DWNL_grdId` y los campos ocultos de WebForms `__VIEWSTATE`/`__EVENTVALIDATION`); `IStorageService` (ya existe) para persistir los PDFs; Dapper + SPs para el registro de extracción.

**Storage**: PostgreSQL vía SPs `usp_*`; reutiliza tablas `licitaciones_adjuntos` y `scraper_sync_log` (V062). Migración nueva: **V077** (columna `metodo_extraccion` + tabla `extraccion_documentos_log`). Cache de sesión: Redis (ya disponible) con TTL.

**Testing**: xUnit + Moq + FluentAssertions (parser de la tabla de adjuntos y de campos WebForms, lógica de fallback, TTL de sesión con `HttpClient` mockeado); integración en `MPM.Tests`. El acceso real al portal no se testea en CI (requiere credenciales y el sitio productivo).

**Target Platform**: Docker Compose (API :5001) / GCP. El servicio corre dentro del proceso de la API como parte del ciclo de sync.

**Project Type**: Web application — monolito modular .NET.

**Performance Goals**: reducir ≥70% el tiempo por licitación vs. navegador (SC-001); sin proceso de navegador por licitación (SC-002). Login: 1 sesión cada ≥N horas en vez de 1 navegación por licitación.

**Constraints**: sin ORM (SPs only); el servicio vive dentro de `MPM.Modules.Licitaciones` sin referenciar otros módulos; migraciones solo vía `Database/Scripts`; el endpoint/estructura interna del portal NO está documentado y debe descubrirse por análisis de tráfico (spike) — el plan asume que ese descubrimiento es viable (supuesto del spec).

**Scale/Scope**: 1 servicio de extracción + 1 proveedor de sesión + 1 parser WebForms + 1 migración; sin cambios de UI. Riesgo concentrado en 2 incógnitas: (a) reproducir la sesión Keycloak de forma cacheable, (b) el postback WebForms de descarga.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Justificación |
|-----------|--------|---------------|
| I. Modular Monolith | ✅ PASS | Todo dentro de `MPM.Modules.Licitaciones`; sin referencias cross-module |
| II. Stored Procedures First | ✅ PASS | Registro de extracción vía nuevos SPs `usp_ExtraccionLog_*` y `usp_Adjuntos_*` con Dapper |
| III. Migraciones embebidas | ✅ PASS | Única migración: `V077__Extraccion_documentos.sql` |
| IV. Multi-Tenancy | ⚠️ N/A justificado | El sync/scraper es un proceso de sistema (no por usuario), igual que `SyncEngineService` actual; no usa `TenantContext` |
| V. Storage abstraction | ✅ PASS | Los PDFs se guardan solo vía `IStorageService` (local/GCS) |
| VI. SignalR + Redis | ✅ PASS | Redis se usa como cache de sesión (uso legítimo del backplane existente); sin nuevos hubs |
| VII. Testing por capas | ✅ PASS | Unit para parser/fallback/TTL; integración para SPs; el acceso real al portal queda como validación manual (quickstart) |

**Post-diseño (re-check)**: ✅ PASS — sin violaciones nuevas. La dependencia AngleSharp es una librería de parsing sin implicancias arquitectónicas.

## Project Structure

### Documentation (this feature)

```text
specs/016-extraccion-documentos-api/
├── plan.md              # Este archivo
├── research.md          # Fase 0 — decisiones técnicas + spike de descubrimiento
├── data-model.md        # Fase 1 — tablas y estructuras
├── quickstart.md        # Fase 1 — guía de validación
├── contracts/
│   └── internal-api.md  # Contratos internos (servicios) y del portal descubierto
└── tasks.md             # Fase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/MPM.Api/Database/Scripts/
└── V077__Extraccion_documentos.sql               # NUEVO: metodo_extraccion + extraccion_documentos_log + SPs

src/MPM.Modules.Licitaciones/
├── Services/
│   ├── MpSessionProvider.cs                       # NUEVO: obtiene/cachea cookies de sesión MP (Keycloak) en Redis
│   ├── AdjuntosHttpExtractor.cs                   # NUEVO: descarga directa por HTTP (listado + postback WebForms)
│   ├── WebFormsParser.cs                          # NUEVO: parseo de #DWNL_grdId y campos __VIEWSTATE/__EVENTVALIDATION (AngleSharp)
│   ├── DocumentExtractionService.cs               # NUEVO: orquesta directo → fallback navegador → registro
│   └── SyncEngineService.cs                       # MOD: invoca DocumentExtractionService en el ciclo de sync
├── Data/
│   ├── ExtraccionLogHandler.cs                    # NUEVO: SPs de registro de extracción
│   └── AdjuntosHandler.cs                         # MOD/uso: persistir adjuntos + metodo_extraccion
└── Models/
    └── ExtraccionModels.cs                        # NUEVO: DTOs (ResultadoExtraccion, AdjuntoDescargado, etc.)

tools/scraper-mp/                                  # SIN CAMBIOS: queda como fallback (invocado por DocumentExtractionService)
```

**Structure Decision**: Se agrega la extracción directa como servicios dentro del módulo existente `MPM.Modules.Licitaciones`, integrada al `SyncEngineService`. El scraper Node se mantiene intacto como mecanismo de respaldo (FR-006) y de comparación en paralelo (FR-008). La sesión (login Keycloak) se obtiene reutilizando el login Node ya funcional a través de un helper invocado por `MpSessionProvider`, evitando reimplementar el flujo OAuth completo en C# (ver research R2).

## Complexity Tracking

| Desviación | Por qué se necesita | Alternativa más simple rechazada porque |
|-----------|---------------------|------------------------------------------|
| `MpSessionProvider` invoca el login Node para obtener cookies | Reproducir el flujo Keycloak (auth code + CSRF + selección de organización) en C# puro es alto riesgo y esfuerzo | Reimplementar Keycloak en C# multiplicaría el riesgo del feature; el login Node ya funciona y se ejecuta 1 vez cada N horas |
| Nueva dependencia AngleSharp | El listado de adjuntos es HTML WebForms con ViewState; parsearlo con regex es frágil | Regex sobre HTML es propenso a romperse ante cambios menores de markup |
