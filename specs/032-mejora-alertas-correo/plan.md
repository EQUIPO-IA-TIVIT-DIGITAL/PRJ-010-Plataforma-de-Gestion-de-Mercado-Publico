# Implementation Plan: Mejora de Alertas por Correo

**Branch**: `032-mejora-alertas-correo` | **Date**: 2026-08-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/032-mejora-alertas-correo/spec.md`

## Summary

Tres mejoras independientes al sistema de alertas por correo, priorizadas por impacto: (1) corregir falsos positivos en el matching de keywords cortas, reemplazando `string.Contains` por una comparación con límites de palabra (regex `\b`); (2) enriquecer el correo de alerta con organismo, fecha de cierre y enlace directo, todos ya disponibles como columnas existentes en `licitaciones` — solo requiere ampliar una proyección y una stored procedure; (3) mover el horario del Cloud Scheduler que dispara sync+alertas de `0 3,15 * * *` a `0 8,15 * * *`, un cambio de infraestructura sin código.

## Technical Context

**Language/Version**: C# / .NET 8 (backend), sin cambios de frontend en esta mejora

**Primary Dependencies**: `System.Text.RegularExpressions` (ya parte de .NET, sin dependencia nueva), Dapper (acceso a datos existente)

**Storage**: PostgreSQL — amplía una stored procedure existente (`usp_Licitaciones_ListarParaMatching`), sin tablas ni columnas nuevas (`fecha_cierre` y `link` ya existen en `licitaciones`)

**Testing**: xUnit + FluentAssertions (`tests/MPM.Modules.Alertas.Tests`, ya existe), pruebas unitarias directas sobre `AlertasMatchingService.EvaluarMatch` (método `internal static`, ya testeable sin mocks pesados)

**Target Platform**: Backend Cloud Run (`mpm-api` service) + Cloud Run Job `sync-job` + Cloud Scheduler (`sync-job-scheduler`)

**Project Type**: Módulo dentro del monolito modular existente (`MPM.Modules.Alertas`), más un cambio de configuración de infraestructura

**Performance Goals**: N/A — el volumen de licitaciones evaluadas por ciclo es bajo (decenas, no miles); el cambio de `Contains` a `Regex.IsMatch` no introduce un costo perceptible a esa escala

**Constraints**: El fix de matching no debe romper coincidencias de frases multi-palabra ya funcionando (FR-002); el enriquecimiento del correo no debe agregar una consulta/round-trip nuevo por licitación (FR ya cubierto por R2 de research.md)

**Scale/Scope**: 3 historias de usuario, cambios acotados a `MPM.Modules.Alertas` (+ 1 método de `LicitacionHandler`/1 stored procedure en `MPM.Modules.Licitaciones`) + 1 cambio de Cloud Scheduler

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Principio II (Stored Procedures First)**: ✅ El campo nuevo se trae ampliando una stored procedure existente (`usp_Licitaciones_ListarParaMatching`), no con ORM ni SQL embebido en C#.
- **Principio III (Migraciones como Scripts Embebidos)**: ✅ La ampliación de la stored procedure se hace vía un nuevo script `VXXX__...sql` en `src/MPM.Api/Database/Scripts/`, siguiendo la convención existente (no se toca la definición de tabla, ambas columnas ya existen).
- **Principio I (Modular Monolith)**: ✅ El cambio de matching/correo vive enteramente en `MPM.Modules.Alertas`; el único cruce de módulo es el mismo que ya existe hoy (`AlertasMatchingService` consumiendo `LicitacionParaMatching`, ya definido en el propio módulo Alertas y poblado por Licitaciones a través del handler existente — no se agrega una referencia nueva entre módulos).
- **Principio VII (Testing por Capas)**: ✅ `EvaluarMatch` ya es `internal static`, testeable con xUnit sin infraestructura — se agregan casos de prueba para los 3 escenarios de aceptación de US1.
- Sin violaciones — no aplica Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/032-mejora-alertas-correo/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── correo-alerta-formato.md
└── tasks.md             # Phase 2 output (/speckit-tasks — no creado por este comando)
```

### Source Code (repository root)

```text
src/MPM.Modules.Alertas/
├── Services/
│   ├── AlertasMatchingService.cs      # EvaluarMatch: Contains -> Regex con \b (US1)
│   └── EmailNotificationService.cs    # EnviarAsync: +organismo, +fechaCierre, +link (US2)
└── Models/
    └── AlertasDtos.cs                 # LicitacionParaMatching: +FechaCierre, +Link

src/MPM.Modules.Licitaciones/
└── Data/
    ├── LicitacionHandler.cs           # ListarParaMatchingAsync/MatchingRow: +2 columnas
    └── LicitacionStoredProcedures.cs  # sin cambios (mismo nombre de SP)

src/MPM.Api/Database/Scripts/
└── VXXX__Ampliar_UspLicitacionesListarParaMatching.sql   # +fecha_cierre, +link al SELECT

tests/MPM.Modules.Alertas.Tests/
└── Services/
    └── AlertasMatchingServiceTests.cs # casos de US1 (match real, falso positivo, frase)

scripts/
└── (fuera de código: gcloud scheduler jobs update — US3, sin archivo de repo asociado
    salvo la documentación en este plan)
```

**Structure Decision**: Todo el trabajo de código vive dentro del monolito modular existente, sin proyectos nuevos — se extiende `MPM.Modules.Alertas` (matching + correo) y se amplía una consulta ya existente en `MPM.Modules.Licitaciones` (fuente de los 2 campos nuevos). El cambio de horario (US3) es puramente de infraestructura (Cloud Scheduler), sin artefactos de código en el repo más allá de quedar documentado en `research.md`/`data-model.md`.

## Complexity Tracking

Sin violaciones al Constitution Check — tabla no aplica.
