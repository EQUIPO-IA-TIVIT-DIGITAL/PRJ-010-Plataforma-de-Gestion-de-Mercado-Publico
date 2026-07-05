# Implementation Plan: Fase 15 — Pricing Intelligence

**Branch**: `012-fase15-pricing-intelligence` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 11 (Noviembre 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Extensión del módulo `MPM.Modules.Analisis` que extrae y agrega datos de precios ofertados desde los JSON de análisis Gemini existentes. Genera un dashboard de benchmarking de precios por categoría y organismo, y un recomendador de precio óptimo con curva precio vs. probabilidad de adjudicación.

---

## Technical Context

**Datos fuente**: `analisis_resultados.contenido_json` → campo `adjudicacion.ofertantes[].oferta_economica`
**Categorías**: Inferidas desde nombre de licitación + tipo + texto del análisis
**Curva precio-probabilidad**: Cálculo estadístico sobre distribución histórica de precios adjudicados
**Estimación**: 1 semana | **Complejidad**: Alta

---

## Module Structure

**Extensión del módulo existente** `MPM.Modules.Analisis`:

```text
src/MPM.Modules.Analisis/
├── Controllers/
│   └── AnalisisController.cs              ← Nuevos endpoints: /pricing/benchmark, /pricing/recomendar
├── Services/
│   ├── PricingBenchmarkService.cs         ← Nuevo: agregaciones de precios
│   └── PricingRecomendadorService.cs      ← Nuevo: curva precio-probabilidad
└── Models/
    └── PricingDtos.cs                     ← BenchmarkDto, RecomendacionPrecioDto

src/MPM.Api/Database/Scripts/
└── V084__SP_Pricing_Intelligence.sql

src/mpm-web/src/
├── pages/PricingDashboardPage.tsx         ← Dashboard de benchmarking
├── components/PricingRecomendador.tsx     ← Widget en detalle oportunidad
└── hooks/usePricing.ts
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Extensión del módulo Analisis |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Pricing_*` |
| **III. Migraciones SQL** | ✅ Aplicar | V084 |
| **IV. Multi-Tenancy** | ✅ Sin violación | Hereda del módulo padre |

---

## Artefactos pendientes

- [ ] `research.md` — categorización de licitaciones, normalización de monedas (CLP/UF)
- [ ] `data-model.md` — BenchmarkCategoria, HistorialPrecio, RecomendacionPrecio
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
