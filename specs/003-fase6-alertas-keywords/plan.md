# Implementation Plan: Fase 6 — Alertas Inteligentes por Palabras Clave

**Branch**: `003-fase6-alertas-keywords` | **Status**: PENDIENTE
**Spec**: [spec.md](./spec.md) | **Semana**: 2 (Julio 2026)
**Actualizado**: 2026-07-03 — alcance ampliado con expansión de sinónimos por IA y notificación enriquecida (ver spec.md, User Stories 3-4)

> Ejecutar `/speckit-plan` para completar: research.md, data-model.md, contracts/, quickstart.md, tasks.md

---

## Summary

Nuevo módulo `MPM.Modules.Alertas` que permite a los usuarios configurar reglas de alerta por palabras clave, monto y tipo de licitación. El motor de matching se ejecuta en cada ciclo de sync de licitaciones, expande cada keyword a sinónimos/conceptos relacionados vía IA, y genera notificaciones in-app con un resumen enriquecido (requisitos, competidores, presupuesto, forma de pago, multas, señal de renovación/proveedor actual) enrutadas a los account managers de gobierno (extensible a email/WA en Fase 10).

---

## Technical Context

**Lenguaje**: .NET 8 + React 18 + TypeScript
**Dependencias nuevas**: Proveedor de IA para expansión de sinónimos y para generar el resumen enriquecido — reutilizar Gemini ya integrado en `MPM.Modules.Analisis` (mismo proveedor que usará el Buscador Inteligente, `018-buscador-inteligente-nl`, para mantener consistencia de conceptos/sinónimos entre ambas features)
**Storage**: PostgreSQL — nuevas tablas `alertas_reglas` y `alertas_disparadas` (`alertas_disparadas` ahora debe persistir también el resumen enriquecido y el término/sinónimo que disparó el match)
**Background Service**: Integración en `SyncEngineService` existente o nuevo `AlertasMatchingService`
**Estimación**: 1.5 semanas (se amplió desde 1 semana original por el alcance de sinónimos IA + enriquecimiento) | **Complejidad**: Media-Alta

---

## Module Structure

**Nuevo módulo**: `MPM.Modules.Alertas`

```text
src/MPM.Modules.Alertas/
├── Controllers/
│   └── AlertasController.cs        ← CRUD de reglas
├── Services/
│   ├── AlertasService.cs           ← Lógica de negocio
│   ├── AlertasMatchingService.cs   ← Motor de matching (Background), expande keywords vía IA
│   └── AlertaEnriquecimientoService.cs ← NUEVO: genera resumen (requisitos, competidores, presupuesto, forma de pago, multas, renovación/proveedor actual)
├── Data/
│   ├── AlertasHandler.cs           ← Dapper queries
│   └── AlertasStoredProcedures.cs  ← Constantes SP
├── Models/
│   └── AlertasDtos.cs              ← ReglaAlertaDto, AlertaDisparadaDto (+ campos de resumen enriquecido)
└── ModuleRegistration.cs

src/MPM.Api/Database/Scripts/
└── V078__Create_Alertas.sql        ← Tablas + SPs (número exacto a confirmar contra la migración más alta al momento de implementar)

src/mpm-web/src/
├── pages/AlertasPage.tsx           ← Lista + formulario de reglas
└── hooks/useAlertas.ts             ← TanStack Query hooks
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Módulo independiente con `AddAlertasModule()` |
| **II. Stored Procedures First** | ✅ Aplicar | `usp_Alertas_*` para todo acceso a BD |
| **III. Migraciones SQL** | ✅ Aplicar | Migración con tablas y SPs, número confirmado al implementar |
| **IV. Multi-Tenancy** | ✅ Aplicar | `usuario_id` en `alertas_reglas`; enrutamiento a account managers respeta tenant |

---

## Artefactos pendientes

- [ ] `research.md` — motor de matching: SQL ILIKE vs. full-text search vs. índice tsvector; estrategia de expansión de sinónimos vía IA (prompt vs. tabla de sinónimos precalculada) compartida con `018-buscador-inteligente-nl`
- [ ] `data-model.md` — entidades: ReglaAlerta, AlertaDisparada (con resumen enriquecido)
- [ ] `contracts/alertas-api.md` — endpoints REST
- [ ] `quickstart.md` — escenarios de validación
- [ ] `tasks.md` — generado con `/speckit-tasks`
