# Implementation Plan: Fase 17 — Gestión Documental de Propuestas

**Branch**: `014-fase17-gestion-documental` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 13 (Diciembre 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Nuevo módulo `MPM.Modules.Documentos` con repositorio de plantillas reutilizables para propuestas de licitación. Control de versiones, categorías, búsqueda full-text, vinculación a oportunidades del pipeline (Fase 7) y checklist de completitud con alertas. Almacenamiento en GCS (mismo `IStorageService`).

---

## Technical Context

**Nuevo módulo**: `MPM.Modules.Documentos`
**Storage**: `IStorageService` existente (GCS en producción, local en dev)
**Control de versiones**: Tabla `documentos_versiones` con historial de uploads
**Búsqueda**: PostgreSQL full-text search (tsvector) en nombre y etiquetas
**Estimación**: 1 semana | **Complejidad**: Alta

---

## Module Structure

```text
src/MPM.Modules.Documentos/
├── Controllers/
│   └── DocumentosController.cs
├── Services/
│   ├── DocumentosService.cs
│   └── DocumentosAlertaService.cs         ← BackgroundService: alertas de completitud
├── Data/
│   ├── DocumentosHandler.cs
│   └── DocumentosStoredProcedures.cs
├── Models/
│   └── DocumentosDtos.cs
└── ModuleRegistration.cs

src/MPM.Api/Database/Scripts/
└── V086__Create_Documentos.sql            ← plantillas, versiones, vinculos_propuesta

src/mpm-web/src/
├── pages/DocumentosPage.tsx               ← Repositorio con búsqueda
├── pages/DocumentoDetallePage.tsx         ← Historial de versiones
└── hooks/useDocumentos.ts
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | `AddDocumentosModule()` independiente |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Documentos_*` |
| **III. Migraciones SQL** | ✅ Aplicar | V086 |
| **IV. Multi-Tenancy** | ✅ Aplicar | `tenant_id` en plantillas |
| **V. Storage** | ✅ Sin violación | Reutiliza `IStorageService` existente |

---

## Artefactos pendientes

- [ ] `research.md` — full-text search en PostgreSQL vs. Elastic, control de versiones de archivos binarios
- [ ] `data-model.md` — Plantilla, Version, Tag, VinculoPropuesta
- [ ] `contracts/documentos-api.md`
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
