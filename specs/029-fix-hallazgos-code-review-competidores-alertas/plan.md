# Implementation Plan: Corrección de hallazgos de code review + QA (Licitaciones / Análisis / Mensajería / Dashboard Ejecutivo / Competidores / Alertas / Scraper v2)

**Branch**: `029-fix-hallazgos-code-review-competidores-alertas` | **Status**: PLANIFICADO (research/data-model/contracts/quickstart completos, falta `tasks.md`)
**Spec**: [spec.md](./spec.md) | **Planificado**: 2026-07-19 | **Actualizado**: 2026-07-19 (incorporados los 12 bugs de QA, no solo los 2 coincidentes)

---

## Summary

Corrige 18 hallazgos antes de que la rama actual (`755c13a` — Buscador NL 018, catálogo 027, scraper v2, módulo Competidores 024) se dé por lista para producción: 8 de un `/code-review` sobre el diff de esa rama, más los 12 bugs de `QA/QA-CU010-Reporte-Hallazgos.docx` (2 de los cuales coinciden con el área ya cubierta por el code review y se fusionan con esos fixes; los otros 10 amplían el alcance a Análisis, Mensajería y Dashboard Ejecutivo).

El trabajo cae en 4 frentes claramente separables por módulo, lo que permite paralelizarlo entre desarrolladores:

1. **Licitaciones** (FR-001, FR-002, FR-009, FR-010, SC-008 regresión) — merge/sync, filtros de fecha (NL y normal), import histórico masivo.
2. **Análisis** (FR-011 a FR-018) — el frente más grande: multi-documento, revocación, moneda, admisibilidad, monto estimado, notificación global, formato, filtro de año del Dashboard Ejecutivo.
3. **Competidores + Alertas** (FR-003, FR-004, FR-005, FR-006, FR-007, FR-008) — igual que el plan original, sin cambios.
4. **Mensajería** (FR-019) — un solo bug, causa raíz ya identificada, el más rápido de cerrar.

**Decisiones clave de esta ronda de planning** (ver `research.md`):

- El cliente Gemini compartido (FR-006) se extrae a `MPM.Shared`, permitido explícitamente por el Principio I de la constitución.
- FR-011 (multi-documento) y FR-012 (detección de revocación) se implementan como una sola pieza de trabajo porque comparten causa raíz — el análisis no tiene contexto de los demás documentos del workspace.
- FR-013/FR-014/FR-015 (moneda, admisibilidad, monto estimado) son ajustes al prompt de extracción de `GeminiService`/`AnalisisService` y su post-procesamiento — no tocan esquema de base de datos.
- FR-010 (import masivo) reutiliza el mecanismo de enriquecimiento que ya existe (`ObtenerPorCodigoAsync` → `apiMpService.GetDetalleAsync` → `ActualizarDetalleAsync`) como job de backfill, en vez de construir uno nuevo.

---

## Technical Context

**Lenguaje**: .NET 8 (C#) + React 18/TypeScript (frontend) + Node.js (scraper `tools/scraper-mp-v2`)
**Módulos afectados**: `MPM.Modules.Licitaciones`, `MPM.Modules.Alertas`, `MPM.Modules.Competidores`, `MPM.Modules.Analisis` (nuevo frente, el más grande), `MPM.Modules.Mensajeria`, `MPM.Shared` (nuevo cliente Gemini compartido), `src/mpm-web` (varios componentes/páginas), `tools/scraper-mp/` (fix opcional de fallback)
**Storage**: PostgreSQL — 1 migración nueva confirmada (FR-001, `codigo_estado`); FR-010 puede requerir 0 migraciones (reusa mecanismo existente) o 1 si se decide marcar explícitamente registros "no recuperables" (ver Edge Cases de la spec) — a confirmar en Phase 0 de research antes de implementar
**Testing**: xUnit + Moq + FluentAssertions para todos los fixes de C#; test manual/documentado en `quickstart.md` para los casos que dependen de comparar contra documentos reales (LP-4609) o del comportamiento del modelo Gemini (FR-011 a FR-015), ya que esos no son deterministas de la misma forma que un bug de tipeo de parámetro
**Fuera de alcance confirmado**: reprocesar retroactivamente licitaciones ya afectadas por el bug de `codigo_estado` (solo si se detectan casos reales); tocar código de `tools/scraper-mp` (v1, deprecado) más allá de no ser alcanzable por accidente; construir un motor de precedencia documental genérico (FR-012 solo exige detectar revocación formal explícita en el texto, no inferir contradicciones no declaradas — ver Edge Cases)
**Estimación**: 3-4 semanas repartidas en los 4 frentes (Licitaciones: 1 semana; Análisis: 1.5-2 semanas, el más grande porque incluye ajustes de prompt que requieren validación iterativa contra casos reales; Competidores+Alertas: 4-5 días; Mensajería: 1-2 días) | **Complejidad**: Media — la mayoría de los fixes son puntuales y acotados, pero FR-011/FR-012 (multi-documento + revocación) y FR-013/FR-014/FR-015 (extracción Gemini) requieren validación contra casos reales, no solo tests unitarios determinísticos

---

## Module Structure

### Frente 1 — Licitaciones

```text
src/MPM.Api/Database/Scripts/
└── VXXX__Fix_MergeLicitaciones_Preserva_Estado_Valido.sql   ← NUEVO (FR-001): reemplaza el fallback a 1 por preservar el estado existente

src/MPM.Modules.Licitaciones/Data/
└── LicitacionHandler.cs                                       ← MODIFICADO (FR-002, FR-009/QA BUG-002): BuscarNaturalAsync recibe fechaDesde real; ListarAsync tipa p_fecha_desde/p_fecha_hasta con DbType.Date explícito
src/MPM.Modules.Licitaciones/Services/
├── LicitacionService.cs                                       ← MODIFICADO (FR-002): pasa interpretacion.FechaDesde
└── ImportBackfillService.cs                                   ← NUEVO (FR-010/QA BUG-003): job de backfill que reusa ObtenerPorCodigoAsync → apiMpService.GetDetalleAsync → ActualizarDetalleAsync sobre los registros del import masivo
src/mpm-web/src/pages/
└── LicitacionesPage.tsx                                       ← MODIFICADO (FR-009/QA BUG-002): distingue error real (500) de "sin resultados"
```

### Frente 2 — Análisis (el más grande)

```text
src/MPM.Modules.Analisis/Services/
├── AnalisisService.cs                                         ← MODIFICADO (FR-011/QA BUG-005): "Analizar todo" procesa todos los documentos, no solo docList.First(); (FR-018/QA BUG-011): filtro de año usa fecha real de licitación, no CreadoEn
├── GeminiService.cs                                           ← MODIFICADO (FR-013/FR-014/FR-015, FR-012): prompt de extracción distingue moneda real, admisibilidad real, monto estimado vs. ofertado, y detección de revocación entre documentos; delega armado/parseo de request a VertexGeminiClient (FR-006, compartido con Competidores)
└── AnalisisFormatoService.cs                                  ← NUEVO o método en servicio existente (FR-017/QA BUG-007): normaliza formato de moneda en backend antes de exponerlo, en vez de confiar en el texto libre del modelo

src/mpm-web/src/pages/
└── AnalisisWorkspacePage.tsx                                  ← MODIFICADO (FR-016/QA BUG-004): mueve el seguimiento de transición de estado (prevEstadoRef) de useRef local a un mecanismo global (contexto/store, o polling a nivel de AppLayout como NotificationBell.tsx)
src/mpm-web/src/components/
└── [componentes del dashboard de análisis]                    ← MODIFICADO (FR-017/QA BUG-007): nombres de métrica no ambiguos, badges de estado consistentes con el texto real
src/mpm-web/src/pages/
└── DashboardEjecutivoPage.tsx (o equivalente)                 ← MODIFICADO (FR-018/QA BUG-011): consume el año real de licitación en vez de CreadoEn del análisis
```

### Frente 3 — Competidores + Alertas (sin cambios respecto al plan original)

```text
src/MPM.Shared/Services/
└── VertexGeminiClient.cs                                       ← NUEVO (FR-006): cliente compartido, extraído de GeminiService, usado también por Análisis (frente 2)

src/MPM.Modules.Competidores/Services/
├── CompetidorGeminiService.cs                                  ← MODIFICADO (FR-003, FR-006): delega a VertexGeminiClient, agrega manejo de candidates vacío
└── CompetidorAnalysisService.cs                                ← MODIFICADO (FR-003): captura y traduce el error de Gemini sin candidatos
src/MPM.Modules.Competidores/Controllers/
└── CompetidoresController.cs                                   ← MODIFICADO (FR-003): devuelve error controlado (ver contracts/)

src/MPM.Modules.Alertas/Data/
└── AlertasHandler.cs                                           ← MODIFICADO (FR-004): agrega AND deleted_at IS NULL

src/mpm-web/src/pages/
└── CompetidoresPage.tsx                                        ← MODIFICADO (FR-007): render de monto distingue null de 0

tools/scraper-mp-v2/modulos/
└── cuadroOfertas.js                                            ← MODIFICADO (FR-005): valida orden de columnas contra headers

src/MPM.Modules.Licitaciones/Services/
└── MpSessionProvider.cs                                        ← MODIFICADO (FR-008): fallback apunta a scraper-mp-v2, no a tools/scraper-mp
```

### Frente 4 — Mensajería

```text
src/mpm-web/src/components/
└── CrearConversacionModal.tsx                                  ← MODIFICADO (FR-019/QA BUG-012): elimina el useMemo roto (selectValue siempre undefined en ambas ramas) o lo corrige para devolver el valor realmente seleccionado; deja que el Form.Item maneje el valor del Select nativamente
```

Migración SQL nueva prevista: 1 archivo confirmado (`VXXX__Fix_MergeLicitaciones_Preserva_Estado_Valido.sql`, FR-001), más una posible migración adicional para FR-010 si se decide marcar registros "no recuperables" explícitamente (a confirmar en research.md antes de implementar) — número exacto a confirmar al implementar contra el máximo real existente.

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | `VertexGeminiClient` va en `MPM.Shared` (compartido entre módulos, no lógica de negocio de un dominio). Cada módulo (Licitaciones, Análisis, Competidores, Alertas, Mensajería) mantiene sus fixes dentro de su propia estructura `Controllers/Services/Data/Models` |
| **II. Stored Procedures First** | ✅ Aplicar | Todos los fixes de datos modifican SPs existentes o el tipeo de una llamada Dapper ya existente — cero SQL ad-hoc nuevo en C#. Los fixes de Análisis (FR-011 a FR-018) son de lógica de aplicación/prompt, no de acceso a datos |
| **III. Migraciones SQL** | ✅ Aplicar | 1 migración confirmada (FR-001); posible 1 adicional para FR-010, a decidir en research.md |
| **IV. Multi-Tenancy** | N/A | Ninguno de los fixes toca resolución de tenant |
| **V. Abstracción de Storage** | N/A | No involucra archivos nuevos — los documentos de Análisis ya pasan por `IStorageService` sin cambios aquí |
| **VII. Testing por Capas** | ✅ Aplicar, con matiz | Los fixes determinísticos (Licitaciones, Competidores, Alertas, Mensajería) llevan unit test estándar. Los fixes de extracción Gemini (FR-011 a FR-015) no son 100% determinísticos — se documentan como casos de validación manual/`quickstart.md` contra los documentos reales que QA ya usó (LP-4609), además de cualquier test unitario que cubra el post-procesamiento no dependiente del modelo (ej. normalización de formato, FR-017) |

Sin violaciones — no requiere justificación adicional en Complexity Tracking.

---

## Artefactos generados en esta ronda (2026-07-19, actualizados con los 12 bugs de QA completos)

- [x] `research.md` — decisiones técnicas por cada uno de los 18 hallazgos, incluye los 4 frentes y el detalle del cruce con QA
- [x] `data-model.md` — entidades afectadas en los 4 frentes (Licitación, Oferta, Análisis de Competidor, Catálogo de estados, Workspace de análisis, Dashboard de análisis, Notificación, Dashboard Ejecutivo, Conversación)
- [x] `contracts/competidores-analisis-api.md` — contrato de error controlado del endpoint de análisis de competidor (FR-003)
- [x] `contracts/analisis-dashboard-moneda.md` — campo de moneda real agregado al JSON del dashboard de Análisis (FR-013)
- [x] `quickstart.md` — 18 escenarios de validación, uno por hallazgo (incluye la verificación de regresión de QA BUG-001 y los casos de validación manual contra documentos reales para el frente de Análisis)
- [ ] `tasks.md` — pendiente, generar con `/speckit-tasks`

## Nota de trazabilidad QA

Los 12 bugs de `QA/QA-CU010-Reporte-Hallazgos.docx` quedan todos incorporados en esta spec:

| QA | Área | Incorporación |
|---|---|---|
| BUG-001 (Estado duplicado) | Licitaciones | Verificación de regresión (SC-008) — ya resuelto por `V108`, presente en esta rama |
| BUG-002 (filtro fecha → 500) | Licitaciones | FR-009, agrupado con FR-002 (mismo archivo) |
| BUG-003 (import masivo tipo/organismo) | Licitaciones | FR-010 |
| BUG-004 (notificación no global) | Análisis | FR-016 |
| BUG-005 (Analizar todo, solo 1er doc) | Análisis | FR-011 |
| BUG-006 (monto estimado confundido) | Análisis | FR-015 |
| BUG-007 (inconsistencias de formato) | Análisis | FR-017 |
| BUG-008 (CLP mostrado como USD) | Análisis | FR-013 |
| BUG-009 (inadmisible mal clasificado) | Análisis | FR-014 |
| BUG-010 (no detecta revocación) | Análisis | FR-012 |
| BUG-011 (filtro año usa fecha análisis) | Dashboard Ejecutivo | FR-018 |
| BUG-012 (no crea conversación directa) | Mensajería | FR-019 |
