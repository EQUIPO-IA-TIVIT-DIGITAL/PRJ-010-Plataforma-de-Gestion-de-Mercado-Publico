# Implementation Plan: Feedback ChileCompra — filtro por área, estadísticas de estado, orden de análisis, competidores ampliado, flujo go/no-go

**Branch**: `031-feedback-chilecompra` | **Status**: PLANIFICADO (research/data-model/contracts/quickstart completos, falta `tasks.md`)
**Spec**: [spec.md](./spec.md) | **Prioridad**: P0 | **Planificado**: 2026-08-04

---

## Summary

Implementa los 5 pedidos de la reunión "[PRJ-010] ChileCompra - Feedback" (2026-08-03) reutilizando al máximo infraestructura ya existente y probada en producción, en vez de construir mecanismos nuevos donde ya hay uno equivalente:

1. **Filtro por área de negocio** (US1) y **estadísticas por estado con drill-down** (US2): ambos se resuelven con el `search_vector` (tsvector GIN-indexed, V066) que ya existe, más un catálogo nuevo pequeño (`areas_negocio`) y extensiones a `usp_Licitaciones_Listar`. Sin IA, sin tabla de clasificación pre-computada.
2. **Orden del historial de análisis por fecha de adjudicación** (US3): reescritura acotada de `usp_AnalisisWorkspaces_Listar` — el JOIN a `licitaciones` ya existe, solo cambia qué se proyecta y el `ORDER BY`.
3. **Informe ejecutivo de competidores ampliado** (US4): el único punto que requiere una capacidad nueva real — extender el scraper para buscar públicamente (no solo "licitaciones en las que TIVIT ofertó"), acotado por área+período para controlar costo, con resultado cacheado y generado en background (mismo patrón de "post-petición" que Manuel describió en la reunión).
4. **Flujo colaborativo go/no-go** (US5): no se construyen tablas de comentarios/asignación nuevas — se reutilizan `conversaciones`/`conversacion_participantes`/`mensajes` de `MPM.Modules.Mensajeria` (que ya tienen un campo `licitacion_id` sin usar, pensado exactamente para esto), coordinadas por un módulo nuevo y pequeño (`MPM.Modules.Colaboracion`) que solo persiste el vínculo entre licitación, workspace de análisis y conversación.

**Decisión clave de esta ronda de planning** (ver `research.md`): la evaluación de infraestructura on-premise mencionada en la misma reunión es explícitamente **no bloqueante** de este plan — el spec la documenta como "Future Considerations" y se sigue tratando por separado.

---

## Technical Context

**Lenguaje**: .NET 8 (backend) + React 18/TypeScript 5 (frontend) — sin cambio de stack
**Storage**: PostgreSQL, 5 migraciones nuevas (V118-V122, confirmar números libres reales al implementar) — ninguna requiere extensión nueva (reutiliza `pg_trgm`/tsvector ya activos)
**Módulos afectados**: `MPM.Modules.Catalogo` (nuevo catálogo `areas_negocio`), `MPM.Modules.Licitaciones` (filtro + estadísticas), `MPM.Modules.Analisis` (orden), `MPM.Modules.Competidores` (actividad de mercado + extensión del scraper Node), `MPM.Modules.Mensajeria` (reutilizado sin cambios de esquema)
**Módulo nuevo**: `MPM.Modules.Colaboracion` (justificado en `data-model.md` — dominio propio: estado de "interés" y vínculo entre licitación/análisis/conversación)
**Scraper**: `tools/scraper-mp-v2` gana un modo de búsqueda pública acotada por área+período (hoy solo busca "licitaciones en las que TIVIT ofertó") — ver `research.md` §4, es el único componente de infraestructura genuinamente nuevo
**Testing**: xUnit + Moq + FluentAssertions por módulo (incluye `tests/MPM.Modules.Colaboracion.Tests` nuevo), Playwright E2E para los 3 flujos de UI nuevos (filtro por área, estadísticas con drill-down, marcar interés → asignar → comentar)
**Constraints de costo**: US4 es explícitamente background/acotado (nunca síncrono, nunca sin límite de área+fecha) — mismo principio que ya rige US5 y que el propio cliente pidió en la reunión ("post-petición", no en vivo)
**Fuera de alcance confirmado**: evaluación de despliegue on-premise (Future Considerations en `spec.md`); clasificación por IA de licitaciones por área (research.md §1, mejora futura si el recall léxico no alcanza)
**Estimación**: 2.5-3 semanas (US1+US2+US3 son ~3-4 días combinadas; US4 es la más incierta por el riesgo de volumen del scraper documentado en `research.md`; US5 es ~1 semana por tocar 3 módulos aunque reutilice esquema existente)
**Complejidad**: Media-Alta — no por dificultad técnica individual de cada historia, sino por tocar 5 módulos + el scraper Node en un solo spec

---

## Module Structure

```text
src/MPM.Modules.Catalogo/
├── Data/CatalogoStoredProcedures.cs      ← agrega AreasNegocio
└── Controllers/CatalogoController.cs      ← GET /catalogos/areas-negocio

src/MPM.Modules.Licitaciones/
├── Controllers/LicitacionController.cs    ← area/sinClasificar en Listar; nuevo GET /estadisticas-estado
├── Services/LicitacionService.cs          ← pasa los filtros nuevos
├── Models/LicitacionFilter.cs             ← + Area, SinClasificar
└── Data/LicitacionStoredProcedures.cs     ← Listar (rewrite), + ContarPorEstado

src/MPM.Modules.Analisis/
├── Data/AnalisisStoredProcedures.cs        ← WorkspacesListar (misma firma, SP rewrite)
└── Models/AnalisisWorkspaceListItemDto.cs  ← + FechaAdjudicacion

src/MPM.Modules.Competidores/
├── Controllers/CompetidoresController.cs         ← GET /{nombre}/actividad-mercado
├── Services/CompetidorMercadoService.cs          ← NUEVO, get-or-generate (mismo patrón que CompetidorAnalysisService)
└── Data/CompetidoresActividadMercadoHandler.cs   ← NUEVO

src/MPM.Modules.Colaboracion/                     ← NUEVO módulo
├── Controllers/LicitacionesInteresController.cs
├── Services/LicitacionesInteresService.cs
├── Data/LicitacionesInteresHandler.cs + LicitacionesInteresStoredProcedures.cs
├── Models/LicitacionInteresDto.cs
└── ModuleRegistration.cs                          ← AddColaboracionModule(), registrado en Program.cs

tools/scraper-mp-v2/modulos/
└── buscarPublico.js                        ← NUEVO — búsqueda pública acotada por área+período (US4), reutiliza cuadroOfertas.js sin cambios

src/mpm-web/src/
├── pages/LicitacionesPage.tsx              ← selector de área + panel de estadísticas con drill-down
├── pages/AnalisisPage.tsx (o equivalente)  ← muestra fechaAdjudicacion, confía en el nuevo orden del backend
├── pages/CompetidoresPage.tsx              ← panel nuevo "Actividad total de mercado" (polling)
├── components/LicitacionInteresPanel.tsx   ← NUEVO — marcar interés, asignar, comentarios (orquesta 3 llamadas, ver contracts/colaboracion-interes.md)
└── hooks/useLicitaciones.ts, useAnalisis.ts, useCompetidores.ts, useLicitacionesInteres.ts (nuevo)
```

Migraciones nuevas: **V118** (`areas_negocio` + seed), **V119** (`usp_Licitaciones_Listar` rewrite + `usp_Licitaciones_ContarPorEstado`), **V120** (`usp_AnalisisWorkspaces_Listar` rewrite), **V121** (`competidores_actividad_mercado` + SPs), **V122** (`licitaciones_interes` + SPs). Confirmar contra `src/MPM.Api/Database/Scripts/` el número real libre al implementar (la última auditoría lo dejó en V118 libre, pero specs anteriores encontraron huecos en la numeración — no asumir contigüidad).

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | Módulo nuevo (`Colaboracion`) tiene responsabilidad de dominio propia y clara; no referencia a `Analisis` ni `Mensajeria` en C#, orquestación ocurre en frontend (ver `research.md` §5) |
| **II. Stored Procedures First** | ✅ Aplicar | Todo acceso nuevo pasa por SPs (`usp_*`) vía Dapper — incluida la clasificación por área (cláusula SQL, no lógica C#) |
| **III. Migraciones SQL** | ✅ Aplicar | 5 migraciones nuevas, numeradas `VXXX__Descripcion.sql`, embebidas — ver `data-model.md` |
| **IV. Multi-Tenancy** | ✅ Sin cambios | Todas las tablas/SPs nuevas siguen recibiendo `TenantContext` donde aplica (igual que el resto del sistema); `licitaciones_interes` y `competidores_actividad_mercado` no son tenant-scoped hoy porque las tablas que extienden (`licitaciones`, `competidores_analisis`) tampoco lo son — confirmar este supuesto en `tasks.md` contra el esquema real antes de implementar |
| **V. Abstracción de Storage** | N/A | Ninguna historia toca archivos/GCS |
| **VI. SignalR + Redis** | ✅ Reutilizado | US5 obtiene tiempo real gratis reusando `mensajes`/SignalR existente — no se agrega mecanismo de push nuevo |
| **VII. Testing por Capas** | ✅ Aplicar | Nuevo proyecto `tests/MPM.Modules.Colaboracion.Tests`; unit tests en los módulos existentes tocados; Playwright E2E para los 3 flujos de UI nuevos |

Sin violaciones que requieran justificación en Complexity Tracking. El único riesgo de diseño no resuelto por completo es de **alcance/costo** (volumen real del scraping acotado en US4), no de arquitectura — documentado como riesgo abierto en `research.md` §4, a validar con una corrida real antes de dar el diseño de US4 por cerrado.

---

## Artefactos generados en esta ronda (2026-08-04)

- [x] `research.md` — 5 decisiones técnicas, cada una basada en una auditoría real del código existente (no en suposiciones)
- [x] `data-model.md` — 3 tablas nuevas, 6 SPs nuevos/reescritos, 1 módulo nuevo justificado contra la Constitución
- [x] `contracts/` — 4 documentos de contrato (licitaciones área+estadísticas, orden de análisis, competidores actividad de mercado, colaboración/interés)
- [x] `quickstart.md` — 6 escenarios de validación end-to-end, uno por historia de usuario
- [ ] `tasks.md` — pendiente, generar con `/speckit-tasks`
