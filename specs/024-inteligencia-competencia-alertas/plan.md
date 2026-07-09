# Implementation Plan: Inteligencia de competencia, alertas interactivas y canal de correo

**Branch**: `024-inteligencia-competencia-alertas` | **Date**: 2026-07-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/024-inteligencia-competencia-alertas/spec.md`

## Summary

Tres mejoras independientes salidas de la reunión de equipo post-demo (2026-07-09): (1) un panel de inteligencia de competencia que recolecta el "Cuadro de Ofertas" público de Mercado Público (confirmado en vivo, sin login) para cualquier licitación adjudicada, permite buscar un competidor y ver todas sus ofertas, y dispara análisis de Gemini **solo bajo demanda** por rango de fechas, cacheado por competidor+periodo; (2) un botón inline "Me interesa" en las alertas de Telegram que responde con un resumen rápido armado desde `ApiMpService.GetDetalleAsync` (ya existe, sin usar hoy), sin IA; (3) un canal de entrega de alertas por correo, reusando `IEmailService`/`SmtpEmailService` ya existente en el proyecto.

## Technical Context

**Language/Version**: C# / .NET 8 (backend), TypeScript 5 + React 18 (frontend), Node.js 20 + Playwright (scraper)

**Primary Dependencies**: Dapper + Npgsql, Google.Cloud.AIPlatform (Gemini vía Vertex AI, ya usado por `MPM.Modules.Analisis`), `IEmailService`/`SmtpEmailService` (ya usado por `MPM.Modules.Auth`), Telegram Bot API (ya usado por `MPM.Modules.Alertas`), Playwright (scraper existente en `tools/scraper-mp/`)

**Storage**: PostgreSQL 15+ — nuevas tablas para ofertas de licitación y análisis de competidor cacheado; extensión de `alertas_destinatarios` para el correo

**Testing**: xUnit + Moq + FluentAssertions (backend), documentación manual para el scraper (no tiene framework de test formal hoy, igual que el resto de `tools/scraper-mp`)

**Target Platform**: Cloud Run (API) + Cloud Run Jobs (scraper) en producción; Docker Compose en local

**Project Type**: Web application (frontend `src/mpm-web` + backend modular monolith + scraper Node.js)

**Performance Goals**: El resumen "Me interesa" debe responder en <10s (SC-004); reutilizar análisis cacheado en <2s (SC-002)

**Constraints**: El análisis de competidor NUNCA se dispara automáticamente (FR-004) — solo por acción explícita del usuario, mostrando el volumen de licitaciones antes de confirmar (FR-006), para no generar costos de Gemini sin control.

**Scale/Scope**: Recolección de ofertas potencialmente sobre las ~126k licitaciones ya sincronizadas (a medida que el scraper las visite); el análisis de IA es acotado por diseño (por competidor+rango, bajo demanda).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Monolith**: US1 y US3 se resuelven dentro de `MPM.Modules.Alertas`/`MPM.Modules.Licitaciones` (ver decisión en research.md R1 sobre si conviene un módulo `MPM.Modules.Competidores` nuevo). US2 extiende `TelegramWebhookController` (ya en `MPM.Modules.Alertas`) y reutiliza `ApiMpService.GetDetalleAsync` (ya en `MPM.Modules.Licitaciones`) — comunicación cross-module vía interfaces inyectadas, sin referencias directas entre módulos.
- **II. Stored Procedures First**: Toda tabla nueva (ofertas, análisis de competidor cacheado, preferencia de correo) se accede vía stored procedures nuevos `usp_<Entidad>_<Verbo>`, sin ORM.
- **III. Migraciones como Scripts Embebidos**: Todo el esquema nuevo va en `VXXX__Descripcion.sql` empezando en **V097**.
- **IV. Multi-Tenancy por Middleware**: Los handlers nuevos reciben `TenantContext` igual que el resto de Alertas/Análisis.
- **V. Abstracción de Storage**: No aplica directamente — por defecto se guardan solo campos estructurados en tablas relacionales, sin archivos nuevos.
- **VI. Real-Time via SignalR + Redis Backplane**: No aplica — estas tres historias son consulta bajo demanda o entrega por canal externo (Telegram/correo), no requieren push por SignalR.
- **VII. Testing por Capas**: Unit tests para stored procedures/handlers nuevos y para la lógica de caché de análisis de competidor y ruteo de correo, siguiendo el patrón ya usado en `MPM.Modules.Alertas.Tests`.

**Resultado**: Sin violaciones. Pendiente de decidir en research.md R1: ¿módulo nuevo `MPM.Modules.Competidores` o extensión de módulos existentes?

## Project Structure

### Documentation (this feature)

```text
specs/024-inteligencia-competencia-alertas/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md
```

### Source Code (repository root)

```text
# US1 — Inteligencia de competencia
tools/scraper-mp/modulos/
└── cuadroOfertas.js                # NUEVO — visita "Cuadro de Ofertas" de una licitación adjudicada, extrae oferentes

src/MPM.Api/Database/Scripts/
├── V097__Create_Licitaciones_Ofertas.sql        # tabla de ofertas por licitación
└── V098__Create_Competidores_Analisis.sql       # tabla de análisis cacheado por competidor+periodo

src/MPM.Modules.Licitaciones/               # o MPM.Modules.Competidores nuevo (ver research.md R1)
├── Controllers/CompetidoresController.cs   # buscar competidor, listar ofertas, disparar/consultar análisis
├── Services/CompetidorAnalysisService.cs   # orquesta Gemini + caché
├── Data/OfertasHandler.cs
└── Data/CompetidorAnalisisHandler.cs

src/mpm-web/src/pages/
└── CompetidoresPage.tsx              # NUEVO panel: buscar, listar ofertas, elegir rango, pedir análisis

# US2 — Telegram "Me interesa"
src/MPM.Modules.Alertas/
├── Controllers/TelegramWebhookController.cs   # extender: manejar callback_query además de /start
└── Services/TelegramNotificationService.cs    # agregar inline keyboard al mensaje de alerta

src/MPM.Modules.Licitaciones/Services/ApiMpService.cs   # reusar GetDetalleAsync (ya existe, sin cambios)

# US3 — Canal de correo
src/MPM.Api/Database/Scripts/
└── V099__Add_Email_Alertas_Destinatarios.sql  # columna email en alertas_destinatarios (ver data-model.md)

src/MPM.Modules.Alertas/Services/
└── EmailNotificationService.cs        # NUEVO — envuelve IEmailService para el formato de alerta

tests/MPM.Modules.Alertas.Tests/
tests/MPM.Modules.Licitaciones.Tests/
└── (tests nuevos por cada pieza, siguiendo el patrón ya usado en 023-fix-bugs-produccion)
```

**Structure Decision**: Se reutiliza al máximo la infraestructura existente — Gemini/Vertex AI ya wireado (Análisis), Telegram ya wireado (Alertas), correo ya wireado (Auth), scraper ya wireado (Licitaciones). La única pieza estructuralmente nueva es dónde vive "Competidores": research.md decide si es un módulo nuevo o una extensión, antes de escribir código.

## Complexity Tracking

*Sin violaciones — sección no aplica.*
