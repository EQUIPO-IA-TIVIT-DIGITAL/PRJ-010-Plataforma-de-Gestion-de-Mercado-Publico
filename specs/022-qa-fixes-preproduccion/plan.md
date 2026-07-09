# Implementation Plan: Corrección de Hallazgos QA Pre-Producción

**Branch**: `022-qa-fixes-preproduccion` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/022-qa-fixes-preproduccion/spec.md`

## Summary

13 hallazgos de la auditoría QA técnica (2026-07-07) siguen presentes en el código, verificados por 6 subagentes de exploración contra el estado actual del repo. El enfoque técnico agrupa las correcciones en 4 frentes que comparten mecanismo, priorizados según el deadline del jueves 9 de julio (Fase 5 — Despliegue en GCP):

1. **Arranque a prueba de fallos** (US1/BUG-001): `pg_advisory_lock` + `throw` en `DatabaseInitializer`.
2. **Durabilidad ante el entorno stateless de Cloud Run** (US2/BUG-002, US3/BUG-004+005, US4/BUG-003+006+007): reemplazar fire-and-forget e in-process workers por mecanismos que sobreviven a la pérdida de CPU/reinicio. Para BUG-002, el estado ya se persiste de forma síncrona hoy (`analisis_workspaces.estado='analizando'` se setea ANTES del `Task.Run`, confirmado en `AnalisisService.cs:99`) — no hace falta cola nueva, solo un `IHostedService` de recuperación que reclama workspaces huérfanos, más `--no-cpu-throttling` en el deploy como mitigación de piso. Para BUG-004/005: gate de `WORKER_MODE` para los workers in-process y `DB_HOST` desde configuración. Para BUG-003/006/007: canary de estructura + lectura paralela de stdout/stderr + enrutamiento de alertas a Telegram en el scraper.
3. **Endurecimiento de seguridad** (US5/BUG-011, US6/BUG-009): allow-list de orígenes CORS + fail-fast de `JWT_SECRET`, y fail-closed del webhook de Telegram.
4. **Negocio y performance** (US7/BUG-010, US8/BUG-008, US9/BUG-012+013): tabla de auditoría `auth_eventos`, reutilizar el patrón `search_vector` de V067 en el listado principal, y dos correcciones puntuales en Alertas (N+1, escape de Markdown + timeout).

Todo el trabajo respeta la Constitución del proyecto: sin ORM (stored procedures + Dapper), migraciones como scripts `VXXX__` embebidos, módulos sin referencias cruzadas, `TenantContext` inyectado, `IStorageService` sin tocar el filesystem directamente.

## Technical Context

**Language/Version**: C# / .NET 8 (backend), Node.js 20 + Playwright (scraper en `tools/scraper-mp/`), TypeScript 5 / React 18 (frontend — sin cambios de UI en este feature)

**Primary Dependencies**: Npgsql 8.x + Dapper 2.x, ASP.NET Core JWT Bearer, StackExchange.Redis, Swashbuckle. Sin dependencias nuevas — todas las correcciones usan primitivas ya presentes en el stack (`pg_advisory_lock` es una función nativa de PostgreSQL, no requiere paquete adicional).

**Storage**: PostgreSQL 15+ (Cloud SQL en producción) vía stored procedures `usp_*`; sin cambios al mecanismo de storage de archivos (`IStorageService`).

**Testing**: xUnit + Moq + FluentAssertions por módulo afectado (`MPM.Core.Tests`, `MPM.Modules.Analisis.Tests`, `MPM.Modules.Licitaciones.Tests`, `MPM.Modules.Alertas.Tests`, `MPM.Modules.Auth.Tests`, `MPM.Api.Tests`); pruebas manuales dirigidas para el scraper Node (no tiene suite automatizada hoy — fuera de alcance agregarla).

**Target Platform**: Cloud Run (servicio web) + Cloud Run Jobs (sync-job, scraper-job) — el entorno stateless y con CPU throttling entre peticiones es la causa raíz común de BUG-002, BUG-004 y BUG-005.

**Project Type**: Web application (backend modular monolith + frontend SPA) — este feature es 100% backend/infraestructura; no hay cambios de UI.

**Performance Goals**: Ver `spec.md` Success Criteria (SC-005 detección de cambio de estructura <10min, SC-010 búsqueda con degradación <20% al duplicar volumen).

**Constraints**: El deploy del jueves 9 de julio 7:59am es una fecha dura; los bloqueantes P1 (US1–US4) deben quedar resueltos y verificables antes de esa fecha. La infraestructura pendiente de terceros (`roles/aiplatform.user`, Cloud SQL IP privada, Memorystore) está fuera de alcance de este feature — ver `specs/002-fase5-deploy-gcp/solicitud-recursos-cloud-run.md`.

**Scale/Scope**: 13 correcciones puntuales distribuidas en 6 módulos (`MPM.Core`/`MPM.Api`, `MPM.Modules.Analisis`, `MPM.Modules.Licitaciones`, `MPM.Modules.Alertas`, `MPM.Modules.Auth`) + `tools/scraper-mp` (Node). Ninguna requiere nueva pantalla de frontend.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Evaluación |
|---|---|
| I. Modular Monolith | ✅ Cumple. Todas las correcciones ocurren dentro del módulo dueño del bug (`Analisis`, `Licitaciones`, `Alertas`, `Auth`, `Core`); ninguna introduce una referencia cruzada entre módulos. La cola durable de análisis (BUG-002) vive dentro de `MPM.Modules.Analisis`; el gate de workers (BUG-004) vive en `MPM.Modules.Licitaciones`. |
| II. Stored Procedures First | ✅ Cumple. `auth_eventos` (BUG-010) se inserta vía un nuevo `usp_Auth_RegistrarEvento`; la recuperación de análisis huérfanos (BUG-002) reutiliza `usp_AnalisisWorkspaces_Listar(p_estado)` y `usp_AnalisisResultados_ObtenerPorWorkspace`, ya existentes, sin proc nuevo; el fix de búsqueda (BUG-008) reutiliza el patrón `search_vector @@ websearch_to_tsquery` ya validado en `usp_Licitaciones_BuscarNatural` (V067). Ningún ORM. |
| III. Migraciones como Scripts Embebidos | ✅ Cumple. Solo 2 migraciones nuevas: `V092` (auth_eventos) y `V093` (fix búsqueda), continuando desde V091. Ver Phase 1 `data-model.md`. |
| IV. Multi-Tenancy por Middleware | ✅ Cumple. El registro de `auth_eventos` recibe el `TenantContext` ya inyectado en `AuthController`; no se lee `HttpContext` directamente en ningún fix nuevo. |
| V. Abstracción de Storage | ✅ N/A — ninguna corrección toca almacenamiento de archivos. |
| VI. Real-Time SignalR + Redis | ✅ N/A — ninguna corrección toca el hub de mensajería. El enrutamiento de alertas del scraper a Telegram (BUG-007) reutiliza `TelegramNotificationService` ya existente en `MPM.Modules.Alertas`, no un mecanismo de push nuevo. |
| VII. Testing por Capas | ✅ Cumple — se exige cobertura unit test por cada corrección de lógica (advisory lock, gate de workers, N+1, escape de Markdown); los cambios de contrato HTTP (ninguno nuevo, salvo posible endpoint de solo-lectura para BUG-010 si se decide exponerlo) requieren test de integración en `MPM.Tests`. |

**Resultado**: Sin violaciones. No se requiere `Complexity Tracking`.

## Project Structure

### Documentation (this feature)

```text
specs/022-qa-fixes-preproduccion/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/MPM.Api/
├── Database/
│   ├── DatabaseInitializer.cs         # BUG-001: pg_advisory_lock + throw
│   └── Database/Scripts/
│       ├── V092__Create_Auth_Eventos.sql              # BUG-010
│       └── V093__Fix_usp_Licitaciones_Listar_Search.sql  # BUG-008 (reusa patrón tsvector de V067)
└── Program.cs                          # BUG-011 (CORS allow-list + JWT fail-fast)

src/MPM.Modules.Analisis/
└── Services/
    ├── AnalisisBackgroundService.cs    # BUG-002: sin cambio de firma; sigue disparando Task.Run (best-effort)
    └── AnalisisRecoveryWorker.cs       # BUG-002 (nuevo IHostedService): reclama workspaces huérfanos en 'analizando'
                                         # sin resultado tras N minutos, reutilizando WorkspacesListar(p_estado) +
                                         # ResultadosObtenerPorWorkspace ya existentes — sin tabla ni proc nuevos

src/MPM.Modules.Licitaciones/
├── ModuleRegistration.cs               # BUG-004: gate por WORKER_MODE/RUN_INPROCESS_WORKERS
├── Services/
│   └── ScraperBackgroundService.cs     # BUG-005 (DB_HOST config), BUG-006 (Task.WhenAll), BUG-007 (Telegram + destinatario válido)
└── (sin migración nueva — V093 arriba corrige el mismo archivo lógico que V006)

src/MPM.Modules.Alertas/
├── Controllers/
│   └── TelegramWebhookController.cs    # BUG-009: fail-closed
└── Services/
    ├── AlertasMatchingService.cs       # BUG-012: N+1
    └── TelegramNotificationService.cs  # BUG-013: escape Markdown + timeout

src/MPM.Modules.Auth/
├── Controllers/
│   └── AuthController.cs               # BUG-010: hook de registro post-login
└── Data/
    └── AuthEventoHandler.cs            # BUG-010: nuevo handler para usp_Auth_RegistrarEvento

tools/scraper-mp/
├── modulos/adjuntos.js                 # BUG-003: canary de estructura
└── agente-mp.js                        # BUG-003: distinguir cupo vs. estructura + alerta

tests/
├── MPM.Core.Tests/                     # BUG-001
├── MPM.Modules.Analisis.Tests/         # BUG-002
├── MPM.Modules.Licitaciones.Tests/     # BUG-004, 005, 006, 007, 008
├── MPM.Modules.Alertas.Tests/          # BUG-009, 012, 013
├── MPM.Modules.Auth.Tests/             # BUG-010
└── MPM.Api.Tests/                      # BUG-011 (CORS/JWT), integración cruzada
```

**Structure Decision**: Se reutiliza la estructura modular existente sin crear módulos nuevos. Solo dos migraciones nuevas, continuando la numeración `VXXX__` desde V092: `V092__Create_Auth_Eventos.sql` (BUG-010) y `V093__Fix_usp_Licitaciones_Listar_Search.sql` (BUG-008). BUG-002 no requiere migración — reutiliza el estado ya persistido en `analisis_workspaces` (ver `research.md`, Decisión R2).

## Complexity Tracking

*Sin violaciones de la Constitution Check — sección no aplica.*

## Constitution Check — Re-evaluación post-diseño (Phase 1)

Tras `research.md` y `data-model.md`, se confirma sin cambios respecto a la evaluación inicial:

- **Principio I (Modular Monolith)**: confirmado — el fix de BUG-007 usa una `ProjectReference` que `MPM.Modules.Licitaciones` **ya tenía** hacia `MPM.Modules.Alertas` (verificado en el `.csproj`); no se agrega ninguna referencia cruzada nueva.
- **Principio II (Stored Procedures First)**: confirmado — un solo proc nuevo (`usp_Auth_RegistrarEvento`) y un `CREATE OR REPLACE` sobre uno existente (`usp_Licitaciones_Listar`); BUG-002 se resuelve reutilizando procs ya existentes, sin ORM en ningún punto.
- **Principio III (Migraciones embebidas)**: confirmado — exactamente 2 archivos `VXXX__`, V092 y V093, numeración continua desde V091.
- **Principios IV-VI**: sin impacto (N/A, confirmado en data-model.md).
- **Principio VII (Testing por capas)**: a reforzar en `/speckit-tasks` — cada corrección de lógica (R1-R13) debe listar su propia tarea de test unitario en el módulo correspondiente.

**Resultado**: Gate superado. Listo para `/speckit-tasks`.
