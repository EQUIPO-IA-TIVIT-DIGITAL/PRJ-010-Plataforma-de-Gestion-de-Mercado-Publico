# Implementation Plan: Ajustes QoL de Frontend + Fix Scraper "0 Resultados"

**Branch**: `030-qol-frontend-y-fix-scraper` | **Date**: 2026-07-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/030-qol-frontend-y-fix-scraper/spec.md`

## Summary

Siete ajustes de calidad de vida sobre el frontend ya rediseñado (spec `019`), más el diagnóstico y corrección de un bug funcional del scraper. El trabajo se agrupa en cuatro frentes técnicos independientes: (1) presentación — desambiguar el ranking de competidores en `/ejecutivo` y agregar filtro/orden/fecha visible en `/analisis`; (2) corrección de datos — timestamps de notificaciones serializados sin zona horaria explícita; (3) tres rediseños visuales dentro del sistema Ant Design ya establecido (`/analisis/:id`, `/analisis/:id/dashboard`, `/alertas`); (4) fix funcional en el scraper Node (`scraper-mp-v2`) — el bucle de búsqueda por estados puede fallar en los 5 estados y reportar "0 licitaciones" como si fuera un resultado legítimo en vez de una falla de lectura, confirmado en `research.md` §1.

## Technical Context

**Language/Version**: C# 12 / .NET 8 (backend), TypeScript 5 / React 18 (frontend), Node.js (ES modules) para el scraper

**Primary Dependencies**: Dapper + Npgsql (backend), Ant Design 5 + TanStack Query v5 + dayjs (frontend), Playwright (scraper `tools/scraper-mp-v2`)

**Storage**: PostgreSQL 15+ vía stored procedures `usp_*` — sin nuevas tablas; ajuste de filtro sobre `analisis_workspaces.created_at` y de serialización sobre `notificaciones.created_at` (ambas `TIMESTAMP` sin zona horaria existentes, ver `research.md` §2)

**Testing**: xUnit + Moq + FluentAssertions (unit, `tests/MPM.Modules.Analisis.Tests`, `tests/MPM.Modules.Notificaciones.Tests`, `tests/MPM.Modules.Licitaciones.Tests`), Playwright E2E (`src/mpm-web/e2e/`) para las pantallas rediseñadas, validación manual del ciclo del scraper contra Mercado Público real (no hay entorno de staging del sitio)

**Target Platform**: Web (navegador) para el frontend; contenedor Docker (Linux) para API y scraper

**Project Type**: Web application (modular monolith .NET 8 + SPA React) — Opción 2 de estructura, ya existente en el repo

**Performance Goals**: Sin objetivo de performance nuevo — los cambios son de presentación, corrección de datos y un fix de confiabilidad; no se espera impacto medible en tiempos de respuesta

**Constraints**: El fix del scraper (US3) no puede probarse contra un entorno de staging de Mercado Público — solo existe el sitio real, con throttling/anti-bot propios (ver `tools/scraper-mp-v2/DEPRECATED.md` y memoria `project_scraper_postback_colgado`). La validación de US3 combina prueba en condiciones reales + simulación de la rama de fallo (forzar 0/5 estados exitosos) sin depender del sitio real para ese caso.

**Scale/Scope**: 3 módulos backend tocados (`MPM.Modules.Analisis`, `MPM.Modules.Notificaciones`, `MPM.Modules.Licitaciones`), 6 páginas frontend tocadas (`EjecutivoDashboardPage`, `NotificacionesPage`, `AnalisisListPage`, `AnalisisWorkspacePage`, `AnalisisDashboardPage`, `AlertasPage`), 1 script Node (`scraper-mp-v2/modulos/buscar.js` + `agente-mp.js`), 1 migración SQL nueva (parámetros de fecha en `usp_AnalisisWorkspaces_Listar`)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Monolith**: ✅ Cada cambio queda dentro de su módulo dueño (`Analisis`, `Notificaciones`, `Licitaciones`); no se cruzan referencias entre módulos.
- **II. Stored Procedures First**: ✅ El filtro por fecha de US4 se implementa extendiendo `usp_AnalisisWorkspaces_Listar` con parámetros nuevos, sin ORM.
- **III. Migraciones como Scripts Embebidos**: ✅ El cambio al SP se entrega como un nuevo `VXXX__*.sql` en `src/MPM.Api/Database/Scripts/`, continuando la numeración desde el más alto existente (ver nota abajo, `030` no fija el número — se confirma en `/speckit-tasks`).
- **IV. Multi-Tenancy por Middleware**: ✅ Ningún cambio toca la extracción de tenant; los endpoints afectados ya reciben `TenantContext` de la forma estándar.
- **V. Abstracción de Storage**: N/A — ningún cambio toca almacenamiento de archivos.
- **VI. Real-Time / SignalR**: N/A — ningún cambio toca el hub de mensajería (`/mensajes` queda fuera de alcance, FR-016).
- **VII. Testing por Capas**: ✅ Se agregan/ajustan unit tests en los módulos tocados; los rediseños visuales se validan con Playwright E2E existente más regresión manual (`quickstart.md`).

Sin violaciones — no aplica `Complexity Tracking`.

## Project Structure

### Documentation (this feature)

```text
specs/030-qol-frontend-y-fix-scraper/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── api-changes.md    # Phase 1 output
└── tasks.md              # Phase 2 output (/speckit-tasks — no creado todavía)
```

### Source Code (repository root)

```text
src/
├── MPM.Modules.Analisis/
│   ├── Controllers/AnalisisController.cs      # + fechaDesde/fechaHasta en ListarWorkspaces (US4)
│   ├── Services/                               # pasa los nuevos parámetros al Data handler
│   └── Data/                                   # llamada a usp_AnalisisWorkspaces_Listar extendido
├── MPM.Modules.Notificaciones/
│   ├── Models/NotificacionesDtos.cs            # serialización UTC explícita de CreatedAt (US2)
│   └── Data/                                    # sin cambio de esquema, solo de mapeo/serialización
├── MPM.Modules.Licitaciones/
│   └── Services/ScraperBackgroundService.cs    # notificación distinta para 0/5 estados exitosos (US3)
├── MPM.Api/Database/Scripts/
│   └── VXXX__Analisis_Workspaces_Filtro_Fecha.sql   # extiende usp_AnalisisWorkspaces_Listar (US4)
└── mpm-web/src/
    ├── pages/EjecutivoDashboardPage.tsx         # US1
    ├── pages/NotificacionesPage.tsx             # US2 (conversión a America/Santiago)
    ├── pages/AnalisisListPage.tsx                # US4
    ├── pages/AnalisisWorkspacePage.tsx           # US5
    ├── pages/AnalisisDashboardPage.tsx           # US6
    └── pages/AlertasPage.tsx                     # US7

tools/scraper-mp-v2/
├── agente-mp.js          # trata 0/5 estados exitosos como fallo de ciclo (US3)
└── modulos/buscar.js     # rastrea éxito por estado, lanza error si 0 de 5 (US3)
```

**Structure Decision**: Se reutiliza la estructura existente del modular monolith (Opción 2 de la plantilla, ya materializada en el repo) — no se crean módulos, proyectos ni carpetas nuevas. El único artefacto nuevo es una migración SQL dentro de `Database/Scripts/`, siguiendo la convención `VXXX__Descripcion.sql` ya vigente.

## Complexity Tracking

*Sin violaciones a la constitución — sección no aplica.*
