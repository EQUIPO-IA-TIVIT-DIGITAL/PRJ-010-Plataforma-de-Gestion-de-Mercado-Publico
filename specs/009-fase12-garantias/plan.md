# Implementation Plan: Fase 12 — Gestión de Garantías

**Branch**: `009-fase12-garantias` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 8 (Septiembre 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Nuevo módulo `MPM.Modules.Garantias` para registro, seguimiento y alertas de garantías bancarias de licitaciones. Se integra con el Pipeline de Oportunidades (Fase 7). Un BackgroundService diario verifica vencimientos y genera alertas a 30/14/7 días.

---

## Technical Context

**Nuevo módulo**: `MPM.Modules.Garantias`
**Monedas soportadas**: CLP, USD, UF (conversión informativa, no en tiempo real)
**Alertas**: Reutiliza `NotificacionesService` existente
**Integración**: Pipeline de Oportunidades (Fase 7) — vincular garantía a oportunidad
**Estimación**: 1 semana | **Complejidad**: Media

---

## Module Structure

```text
src/MPM.Modules.Garantias/
├── Controllers/
│   └── GarantiasController.cs
├── Services/
│   ├── GarantiasService.cs
│   └── GarantiasVencimientoService.cs  ← BackgroundService diario
├── Data/
│   ├── GarantiasHandler.cs
│   └── GarantiasStoredProcedures.cs
├── Models/
│   └── GarantiasDtos.cs
└── ModuleRegistration.cs

src/MPM.Api/Database/Scripts/
└── V081__Create_Garantias.sql

src/mpm-web/src/
├── pages/GarantiasPage.tsx            ← Dashboard + tabla
├── pages/GarantiaDetallePage.tsx      ← Detalle + historial
└── hooks/useGarantias.ts
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | `AddGarantiasModule()` independiente |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Garantias_*` |
| **III. Migraciones SQL** | ✅ Aplicar | V081 |
| **IV. Multi-Tenancy** | ✅ Aplicar | `tenant_id` en garantías |

---

## Artefactos pendientes

- [ ] `research.md` — estados de garantía, conversión UF informativa
- [ ] `data-model.md` — Garantia, TipoGarantia, HistorialEstado
- [ ] `contracts/garantias-api.md`
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
