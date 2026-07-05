# Implementation Plan: Fase 7 — Pipeline de Oportunidades

**Branch**: `004-fase7-pipeline-oportunidades` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 3 (Julio 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Nuevo módulo `MPM.Modules.Pipeline` que implementa un tablero Kanban de oportunidades comerciales. Cada licitación de interés puede agregarse al pipeline y avanzar por 6 etapas, con responsable asignado, checklist de documentos y registro de resultado. Es el hub central desde el que se enlazan Análisis de Bases (Fase 8), Garantías (Fase 12) y Gestión Documental (Fase 17).

---

## Technical Context

**Lenguaje**: .NET 8 + React 18 + TypeScript
**Dependencias nuevas frontend**: `@dnd-kit/core` o `react-beautiful-dnd` para drag-and-drop del Kanban
**Storage**: PostgreSQL — tablas `oportunidades`, `oportunidades_checklist`, `oportunidades_notas`
**Estimación**: 1 semana | **Complejidad**: Alta (drag-and-drop + lógica de estados)

---

## Module Structure

**Nuevo módulo**: `MPM.Modules.Pipeline`

```text
src/MPM.Modules.Pipeline/
├── Controllers/
│   └── PipelineController.cs       ← CRUD oportunidades + cambio de estado
├── Services/
│   ├── PipelineService.cs          ← Lógica de negocio + validaciones
│   └── PipelineNotificacionesService.cs ← Alertas de vencimiento
├── Data/
│   ├── PipelineHandler.cs
│   └── PipelineStoredProcedures.cs
├── Models/
│   └── PipelineDtos.cs
└── ModuleRegistration.cs

src/MPM.Api/Database/Scripts/
└── V076__Create_Pipeline.sql

src/mpm-web/src/
├── pages/PipelinePage.tsx          ← Kanban principal
├── pages/OportunidadPage.tsx       ← Detalle con checklist
└── hooks/usePipeline.ts
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | `AddPipelineModule()` independiente |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Pipeline_*` para toda la BD |
| **III. Migraciones SQL** | ✅ Aplicar | V076 |
| **IV. Multi-Tenancy** | ✅ Aplicar | `tenant_id` en oportunidades |

---

## Artefactos pendientes

- [ ] `research.md` — librería drag-and-drop, estado máquina de 6 etapas
- [ ] `data-model.md` — Oportunidad, Checklist, Nota, Historial de Estado
- [ ] `contracts/pipeline-api.md`
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
