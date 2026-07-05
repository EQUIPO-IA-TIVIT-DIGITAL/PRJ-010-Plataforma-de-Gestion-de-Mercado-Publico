# Implementation Plan: Fase 11 — Inteligencia Competitiva Avanzada

**Branch**: `008-fase11-inteligencia-competitiva` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 7 (Septiembre 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Extensión del módulo `MPM.Modules.Analisis` y del `EjecutivoDashboardPage` existente. Agrega perfiles de competidor con KPIs profundos, tabla de enfrentamientos directos TIVIT vs. competidor, panel de patrones de pérdida y comparativa lado a lado. Todos los datos se derivan de los `analisis_resultados.contenido_json` existentes.

---

## Technical Context

**Base de datos**: Todos los datos vienen de `analisis_resultados.contenido_json` (ya existente)
**Nuevas queries**: Agregaciones sobre JSON (`jsonb_to_recordset`, `json_array_elements`) en PostgreSQL
**Frontend**: Nuevas páginas y componentes Recharts/Ant Design
**Estimación**: 1 semana | **Complejidad**: Alta (queries JSON complejas + múltiples vistas)

---

## Module Structure

**Extensión del módulo existente** `MPM.Modules.Analisis`:

```text
src/MPM.Modules.Analisis/
├── Controllers/
│   └── AnalisisController.cs          ← Nuevos endpoints: /competidores, /competidores/:nombre, /patrones
├── Services/
│   └── CompetidorAnalisisService.cs   ← Nuevo: agregaciones de competidores
├── Data/
│   └── CompetidorHandler.cs           ← Nuevo: queries JSON profundas
└── Models/
    └── CompetidorDtos.cs              ← Nuevo: PerfilCompetidorDto, PatronPerdidaDto

src/MPM.Api/Database/Scripts/
└── V080__SP_Competidores_Analisis.sql ← Nuevas funciones de agregación

src/mpm-web/src/
├── pages/CompetidorPerfilPage.tsx     ← Nuevo: perfil completo
├── pages/PatronesPerdidaPage.tsx      ← Nuevo: análisis de patrones
└── hooks/useCompetidores.ts           ← Nuevo
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Extensión del módulo Analisis |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Competidores_*` para queries JSON |
| **III. Migraciones SQL** | ✅ Aplicar | V080 |
| **IV. Multi-Tenancy** | ✅ Sin violación | Hereda del módulo padre |

---

## Artefactos pendientes

- [ ] `research.md` — queries JSON eficientes en PostgreSQL para arrays de ofertantes
- [ ] `data-model.md` — PerfilCompetidor, Enfrentamiento, PatronPerdida
- [ ] `contracts/competidores-api.md`
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
