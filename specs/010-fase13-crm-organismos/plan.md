# Implementation Plan: Fase 13 — CRM de Organismos Compradores

**Branch**: `010-fase13-crm-organismos` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 9 (Octubre 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Nuevo módulo `MPM.Modules.CRM` con fichas de organismos compradores del estado, directorio de contactos y línea de tiempo de interacciones. Los KPIs por organismo se calculan automáticamente desde los análisis existentes. Se integra con el Pipeline de Oportunidades (Fase 7) para enriquecimiento bidireccional.

---

## Technical Context

**Nuevo módulo**: `MPM.Modules.CRM`
**Enriquecimiento automático**: Parsear `organismo_comprante` de `licitaciones` existentes para auto-crear fichas
**KPIs calculados**: Queries sobre `analisis_resultados` + `licitaciones` agrupadas por organismo
**Estimación**: 1 semana | **Complejidad**: Alta

---

## Module Structure

```text
src/MPM.Modules.CRM/
├── Controllers/
│   ├── OrganismosController.cs
│   └── ContactosController.cs
├── Services/
│   ├── CRMService.cs
│   └── OrganismoEnriquecimientoService.cs  ← Auto-crea fichas desde licitaciones
├── Data/
│   ├── CRMHandler.cs
│   └── CRMStoredProcedures.cs
├── Models/
│   └── CRMDtos.cs
└── ModuleRegistration.cs

src/MPM.Api/Database/Scripts/
└── V082__Create_CRM.sql                    ← organismos, contactos, notas, interacciones

src/mpm-web/src/
├── pages/OrganismosPage.tsx               ← Lista con búsqueda
├── pages/OrganismoDetallePage.tsx         ← Ficha completa
├── pages/ContactosPage.tsx                ← Directorio global
└── hooks/useCRM.ts
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | `AddCRMModule()` independiente |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_CRM_*` |
| **III. Migraciones SQL** | ✅ Aplicar | V082 |
| **IV. Multi-Tenancy** | ✅ Aplicar | `tenant_id` en todas las tablas |

---

## Artefactos pendientes

- [ ] `research.md` — normalización de nombres de organismos del estado chileno
- [ ] `data-model.md` — Organismo, Contacto, Nota, Interaccion
- [ ] `contracts/crm-api.md`
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
