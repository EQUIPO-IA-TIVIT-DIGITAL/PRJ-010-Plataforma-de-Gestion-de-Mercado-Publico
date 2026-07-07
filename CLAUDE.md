# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **Note**: This repo root also contains an unrelated TIVIT "Framework Agéntico" for OpenCode (`AGENTS.md`, `.opencode/`, `opencode.json`, `README.md`). That framework is not part of the MPM application — the MPM source lives under `src/` and `tests/`, described below.

## Project Overview

**MPM (Mercado Público Management)** — A .NET 8 modular monolith + React frontend for managing and analyzing Chilean public procurement tenders (licitaciones) from [mercadopublico.cl](https://www.mercadopublico.cl). The system syncs tenders via the Mercado Público API, supports internal messaging, and uses Gemini AI to analyze PDF evaluation documents.

## Commands

### Backend (.NET 8)

```bash
# Build entire solution
dotnet build MPM.sln

# Run API (development)
dotnet run --project src/MPM.Api

# Run all tests
dotnet test MPM.sln

# Run tests for a specific project
dotnet test tests/MPM.Modules.Analisis.Tests

# Run a single test by name
dotnet test tests/MPM.Tests --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

### Frontend (React + Vite)

```bash
cd src/mpm-web

npm install
npm run dev          # Dev server on http://localhost:3000
npm run build        # Production build
npm run test:e2e     # Playwright E2E tests (headless)
npm run test:e2e:ui  # Playwright with UI runner
```

### Docker (full stack)

```bash
# Requires a .env file at the root (see docker-compose.yml for variables)
docker compose up --build
# API → http://localhost:5001   Web → http://localhost:8181   DB → localhost:5433
```

### Database migrations

Migrations run automatically on startup via `DatabaseInitializer`. To add a migration, create a `.sql` file in `src/MPM.Api/Database/Scripts/` following the naming convention `VXXX__Description.sql` (e.g. `V075__Add_something.sql`, continuing from the highest existing `VXXX`). The file is embedded as a resource and applied in alphabetical order.

## Architecture

### Backend — Modular Monolith

Each domain is a separate class library with an `AddXxxModule()` extension method registered in `Program.cs`:

| Module | Responsibility |
|--------|---------------|
| `MPM.Shared` | Shared models (`TenantContext`, etc.), `IStorageService` |
| `MPM.Core` | `DbConnectionFactory`, `ErrorHandlingMiddleware`, `TenantMiddleware` |
| `MPM.Modules.Auth` | JWT auth, password reset, user validation |
| `MPM.Modules.Licitaciones` | Tender sync from Mercado Público API, scraper background service, search |
| `MPM.Modules.Catalogo` | Reference data (estados, tipos, monedas) |
| `MPM.Modules.Mensajeria` | Real-time chat via SignalR (`/hubs/mensajeria`) |
| `MPM.Modules.Analisis` | Workspace for uploading PDF documents, Gemini AI analysis, chat Q&A |
| `MPM.Modules.Notificaciones` | In-app notifications |

**Module structure** (consistent across all modules):
```
MPM.Modules.Xxx/
  Controllers/     HTTP endpoints
  Services/        Business logic + background services
  Data/            DB handlers (raw Dapper calls) + stored procedure name constants
  Models/          DTOs
  ModuleRegistration.cs
```

**Database access**: All queries go through PostgreSQL stored procedures (named `usp_*`) called via Dapper. `Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true` is set globally. The `DbConnectionFactory` creates `NpgsqlConnection`s.

**Multi-tenancy**: `TenantMiddleware` extracts `user_id`, `tenant_id`, `username`, `roles`, and `tenant_name` from JWT claims and stores them in `HttpContext.Items["TenantContext"]`.

**Storage**: `IStorageService` has two implementations — `LocalStorageService` (default, writes to `/app/uploads`) and `GcsStorageService` (Google Cloud Storage). Switched via `Storage:Provider` config (`local` or `gcs`).

**Background services**:
- `SyncEngineService` — runs daily to sync licitaciones from the Mercado Público API
- `ScraperBackgroundService` — scrapes additional tender attachments
- `AnalisisBackgroundService` — queues PDF analysis jobs through Gemini

**SignalR**: Uses Redis backplane (`AddStackExchangeRedis`). The JWT token for SignalR is passed via query string `?access_token=` (handled in `JwtBearerEvents.OnMessageReceived`).

### Frontend — React 18 + TypeScript

**Routing** (`App.tsx`): React Router 6 with a `ProtectedRoute` wrapper. All authenticated pages share `AppLayout`.

**Data fetching**: TanStack Query (`@tanstack/react-query`) throughout. Each feature has a dedicated hook in `src/hooks/` (e.g. `useLicitaciones`, `useAnalisis`, `useConversaciones`).

**Real-time**: `useSignalR` hook wraps `@microsoft/signalr` for the messaging hub.

**Dev proxy**: Vite proxies `/api` → `http://localhost:5001` and `/hubs` → `http://localhost:5001` (con soporte WebSocket) — corregido 2026-07-07, apuntaba a `:5000`, un puerto que no coincidía ni con el mapeo de Docker (`5001:80`) ni con `dotnet run` (`launchSettings.json`: 5147/7059); el login del frontend fallaba silenciosamente contra ese puerto.

**UI library**: Ant Design 5 (`antd`) with `@ant-design/icons`.

**Key pages**:
- `/licitaciones` — filterable/searchable tender list with sync trigger
- `/analisis` → `/analisis/:id` → `/analisis/:id/dashboard` — three-step analysis flow (list → workspace → results dashboard)
- `/mensajes` — real-time chat with presence indicators
- `/catalogos` — reference data management

### Environment variables (`.env`)

Key variables used by `docker-compose.yml` and the API:
- `DB_USER`, `DB_PASSWORD`, `DB_NAME` — PostgreSQL
- `REDIS_PASSWORD`
- `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`
- `MP_TICKET` — Mercado Público API authentication ticket
- `GEMINI_API_KEY` — Google Gemini API key
- `Storage__Provider` — `local` or `gcs`
- `Storage__Bucket` — GCS bucket name (when using GCS)

### Testing

- **Unit/integration**: xUnit + Moq + FluentAssertions. Test projects mirror src modules (`tests/MPM.Modules.Xxx.Tests`, plus `MPM.Api.Tests`, `MPM.Core.Tests`, `MPM.Shared.Tests`).
- **Integration tests** (`tests/MPM.Tests`): Uses `Microsoft.AspNetCore.Mvc.Testing` with `WebApplicationFactory`.
- **E2E**: Playwright in `src/mpm-web/e2e/`, configured via `playwright.config.ts`.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan at:
`specs/002-fase5-deploy-gcp/plan.md`

Current features (dos en paralelo, ambas con deadline jueves 9 de julio 2026, 7:59 a.m.):

1. **Fase 5 — Despliegue en GCP** (N1): `specs/002-fase5-deploy-gcp/plan.md` — pivote de Compute Engine a **Cloud Run + Cloud Run Jobs**. `016-extraccion-documentos-api` ya NO es bloqueante (Cloud Run Jobs no throttlean CPU, corren el scraper con Chromium igual de largo que hoy). Bloqueado por infraestructura pendiente de Nicolás Valdivia (VPC custom, mover Cloud SQL, Memorystore, roles de IAM) — ver `specs/002-fase5-deploy-gcp/solicitud-recursos-cloud-run.md`. Código de la app (modo worker, deploy.sh, setup-secrets.sh) ya listo, ver `docs/runbook-produccion.md`.
2. **Fase 6 — Alertas Inteligentes** (N2): `specs/003-fase6-alertas-keywords/plan.md` — es la entrega a demostrar el jueves (cadencia semanal de demos). Incluye User Story 5 nueva (bot de Telegram, instrucción interna de Manuel) y endpoint de "disparar alerta de prueba" para demo. `tasks.md` pendiente de generar con `/speckit-tasks`.

> Repriorización 2026-07-03, ajustada 2026-07-06 (ver `specs/ROADMAP.md`, incluye export `specs/roadmap.csv`): orden vigente de fases activas —
> **N1** Fase 5 — Despliegue en GCP (`002-fase5-deploy-gcp`) →
> **N2** Fase 6 — Alertas Inteligentes (`003-fase6-alertas-keywords`, este es el entregable de la demo del jueves) →
> **N3** Buscador Inteligente NL (`018-buscador-inteligente-nl`) →
> **N4** Fase 7 — Pipeline de Oportunidades (`004-fase7-pipeline-oportunidades`, incluye el motor de validación de completitud pedido por el cliente).
> En paralelo o después de N4, sin fecha fija y sin desplazar las anteriores: Rediseño Frontend (`019-rediseno-frontend`).
> Las Fases 8-18 (`005` a `015`) quedan **pausadas** — no son punto de dolor del cliente por ahora.
>
> Nota: el feature 017 (Ajustes Urgentes del Cliente) ya fue implementado; su spec sigue en `specs/017-ajustes-urgentes-cliente/`. La migración más alta aplicada es **V088** — V079 fue tomada por `003-fase6-alertas-keywords` (`usp_Alertas_*`); V080-V088 corrigen bugs reales del pipeline de sync/backfill de licitaciones (migraciones duplicadas, tipos Npgsql, catálogo de estados, matching de Alertas — ver `specs/021-scraper-tivit-hardening/spec.md` y el historial de `src/MPM.Api/Database/Scripts/`). La siguiente libre es **V089**.
>
> Nota (2026-07-07): se agregó `specs/021-scraper-tivit-hardening/` — hardening del scraper de TIVIT (`tools/scraper-mp/`) para identificar y analizar con Gemini **todas** las licitaciones donde TIVIT participó (33 confirmadas en 2025-2026, no solo 10 — había un bug de paginación). Incluye un orquestador de sesiones que reintenta automáticamente cuando Mercado Público agota el cupo de la acción "Ver Adjuntos" (~15 usos por ventana de tiempo), sin intervención manual. Pendiente: hornear `Xvfb`+`xauth` en el Dockerfile para que esto funcione igual en Cloud Run Jobs (ver también `specs/002-fase5-deploy-gcp/research.md` §1b, riesgo de reCAPTCHA en modo headless).
<!-- SPECKIT END -->
