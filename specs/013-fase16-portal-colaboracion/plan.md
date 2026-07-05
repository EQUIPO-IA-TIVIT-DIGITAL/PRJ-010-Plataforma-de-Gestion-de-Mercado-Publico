# Implementation Plan: Fase 16 — Portal de Revisión Externa

**Branch**: `013-fase16-portal-colaboracion` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 12 (Noviembre 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Extensión del módulo `MPM.Modules.Analisis` para generar links de acceso temporal a análisis de licitación. Los links son URLs únicas con token UUID que permiten acceso de solo-lectura sin login. Incluye expiración configurable, revocación manual y registro de accesos.

---

## Technical Context

**Extensión del módulo existente**: `MPM.Modules.Analisis`
**Token**: UUID v4 almacenado en tabla `analisis_links_compartidos`
**Vista pública**: Nueva ruta React `/share/:token` sin `ProtectedRoute`
**Seguridad**: Token de un solo uso por sesión no, pero con expiración estricta y rate limiting
**Estimación**: 1 semana | **Complejidad**: Media

---

## Module Structure

**Extensión del módulo existente** `MPM.Modules.Analisis`:

```text
src/MPM.Modules.Analisis/
├── Controllers/
│   ├── AnalisisController.cs              ← Nuevos endpoints: /compartir, /links
│   └── AnalisisPublicoController.cs       ← Nuevo: endpoint público sin [Authorize]
└── Models/
    └── LinkCompartidoDto.cs               ← Nuevo

src/MPM.Api/Database/Scripts/
└── V085__Create_Links_Compartidos.sql

src/mpm-web/src/
├── pages/AnalisisPublicoPage.tsx          ← Nueva: vista pública sin AppLayout
├── App.tsx                                ← Nueva ruta /share/:token (pública)
└── hooks/useAnalisisPublico.ts
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Extensión del módulo Analisis |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_AnalisisLinks_*` |
| **III. Migraciones SQL** | ✅ Aplicar | V085 |
| **IV. Multi-Tenancy** | ✅ Sin violación | Link pertenece a `tenant_id` del creador |
| **Seguridad** | ⚠️ Revisar | Vista pública expone datos — solo lectura, sin datos de usuarios |

---

## Artefactos pendientes

- [ ] `research.md` — seguridad de links públicos, rate limiting en endpoint público
- [ ] `data-model.md` — LinkCompartido, RegistroAcceso
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
