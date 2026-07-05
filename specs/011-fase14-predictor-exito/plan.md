# Implementation Plan: Fase 14 — Predictor de Éxito

**Branch**: `011-fase14-predictor-exito` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 10 (Octubre 2026)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Motor de scoring en `MPM.Modules.Analisis` que calcula la probabilidad (0-100%) de que TIVIT gane una licitación. En la primera versión usa reglas heurísticas basadas en historial (no ML puro). Los factores: historial en el organismo, tipo de licitación, rango de monto, competidores esperados, y fortalezas/debilidades extraídas de análisis previos. Incluye simulador de precio y track record del predictor.

---

## Technical Context

**Enfoque inicial**: Motor de reglas + scoring ponderado (no ML/Python en v1)
**Datos fuente**: `analisis_resultados.contenido_json` + `oportunidades` (Fase 7) + `licitaciones`
**Dependencias**: Requiere Fase 11 (inteligencia competitiva) para datos de competidores
**API Gemini**: Puede usarse para enriquecer el análisis con razonamiento contextual
**Estimación**: 1 semana | **Complejidad**: Muy Alta

---

## Module Structure

**Extensión del módulo existente** `MPM.Modules.Analisis`:

```text
src/MPM.Modules.Analisis/
├── Controllers/
│   └── AnalisisController.cs              ← Nuevo endpoint: POST /predecir
├── Services/
│   ├── PredictorService.cs                ← Nuevo: motor de scoring
│   └── PredictorSimuladorService.cs       ← Nuevo: "¿qué pasa si bajo precio?"
└── Models/
    └── PredictorDtos.cs                   ← PrediccionDto, FactorDto, SimulacionDto

src/MPM.Api/Database/Scripts/
└── V083__Create_Predicciones.sql          ← Tabla historial de predicciones

src/mpm-web/src/
├── components/PredictorWidget.tsx         ← Widget en detalle de oportunidad
├── pages/PredictorMetricasPage.tsx        ← Precisión histórica
└── hooks/usePredictor.ts
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Extensión del módulo Analisis |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Predicciones_*` |
| **III. Migraciones SQL** | ✅ Aplicar | V083 |
| **IV. Multi-Tenancy** | ✅ Sin violación | Hereda del módulo padre |

---

## Artefactos pendientes

- [ ] `research.md` — modelo de scoring: pesos de factores, normalización, calibración
- [ ] `data-model.md` — Prediccion, Factor, SimulacionParametros
- [ ] `quickstart.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`
