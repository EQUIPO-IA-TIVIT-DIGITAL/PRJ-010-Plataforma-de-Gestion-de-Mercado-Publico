# Implementation Plan: Fase 9 — Reportes Ejecutivos Automáticos

**Branch**: `006-fase9-reportes-ejecutivos` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 5 (Agosto 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Nuevo módulo `MPM.Modules.Reportes` con un BackgroundService cron que genera reportes ejecutivos PDF y Excel cada miércoles a las 20:00 y los envía por email. Los reportes incluyen KPIs del pipeline, win rate, competidores y próximos cierres. También se expone un endpoint on-demand para generación manual desde el frontend.

---

## Technical Context

**Lenguaje**: .NET 8 + React 18
**Dependencias nuevas**:
- `QuestPDF` (NuGet) — generación de PDF en .NET, sin dependencias externas
- `ClosedXML` (NuGet) — generación de Excel (.xlsx)
- Email: servicio SMTP existente (reutilizar `IEmailService` del módulo Auth)
**Cron**: `BackgroundService` con timer o `NCrontab` para schedule preciso
**Estimación**: 1 semana | **Complejidad**: Media

---

## Module Structure

**Nuevo módulo**: `MPM.Modules.Reportes`

```text
src/MPM.Modules.Reportes/
├── Controllers/
│   └── ReportesController.cs         ← GET /api/v1/reportes, POST /generar
├── Services/
│   ├── ReportesService.cs            ← Orquestación y lógica de datos
│   ├── PdfGeneratorService.cs        ← QuestPDF templates
│   ├── ExcelGeneratorService.cs      ← ClosedXML templates
│   └── ReportesCronService.cs        ← BackgroundService cron miércoles 20:00
├── Data/
│   ├── ReportesHandler.cs
│   └── ReportesStoredProcedures.cs
├── Models/
│   └── ReportesDtos.cs
└── ModuleRegistration.cs

src/MPM.Api/Database/Scripts/
└── V078__Create_Reportes.sql         ← Tabla historial de reportes generados

src/mpm-web/src/
├── pages/ReportesPage.tsx            ← Historial + descarga on-demand
└── hooks/useReportes.ts
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | `AddReportesModule()` independiente |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Reportes_*` para queries de datos |
| **III. Migraciones SQL** | ✅ Aplicar | V078 |
| **IV. Multi-Tenancy** | ✅ Aplicar | Reporte por `tenant_id` |

---

## Artefactos pendientes

- [ ] `research.md` — QuestPDF vs. iTextSharp, cron preciso en .NET
- [ ] `data-model.md` — ReporteGenerado, ConfiguracionReporte
- [ ] `contracts/reportes-api.md`
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
