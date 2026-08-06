---

description: "Task list for 031-feedback-chilecompra"
---

# Tasks: Feedback ChileCompra — filtro por área, estadísticas de estado, orden de análisis, competidores ampliado, flujo go/no-go

**Input**: Design documents from `specs/031-feedback-chilecompra/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: se incluyen tareas de unit test (xUnit+Moq+FluentAssertions, convención del proyecto — ver `CLAUDE.md`) para la lógica de servicio y las cláusulas SQL nuevas. No se incluyen tests de red real contra Mercado Público para el scraper (US4) — mismo criterio que el resto del repo con Vertex AI/scraping real: se valida en vivo vía `quickstart.md`, no en CI.

**Organización**: por user story de `spec.md`. Orden de fases: **US1 y US3 (P1)** primero, **US2 y US5 (P2)** después, **US4 (P3)** al final — coincide con el orden de riesgo/incertidumbre documentado en `plan.md` (US4 es la única historia con una capacidad de infraestructura nueva no verificada).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede ejecutar en paralelo (archivos distintos, sin dependencias entre sí)
- **[Story]**: US1-US5 según `spec.md`
- Todas las rutas son relativas a la raíz del repo

---

## Phase 1: Setup

**Purpose**: confirmar contra el estado real del repo lo que `research.md`/`plan.md` solo pudieron estimar — no hay inicialización de proyecto nueva, los módulos base ya existen.

- [X] T001 Confirmar la última migración aplicada listando `src/MPM.Api/Database/Scripts/V*.sql` — usar como base real para numerar V118+ en vez de asumir que no hay huecos (specs anteriores encontraron huecos en la numeración, ver `research.md`)
- [X] T002 [P] Investigación en vivo (Claude in Chrome o script standalone, mismo enfoque que la investigación de `buscar.js` en la sesión anterior): confirmar en `https://www.mercadopublico.cl/BID/Modules/RFB/NEwSearchProcurement.aspx` cuál es el radio/filtro que corresponde a "todas las licitaciones públicas" (no el radio `#radLicitacionOfertado` que usa `buscar.js` hoy) y si requiere sesión autenticada o es accesible público — esto determina el diseño real de `buscarPublico.js` en la Fase 7 (US4)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: construir la clasificación por área de negocio — la necesitan tanto US1 como US2 (y US4 la reutiliza para acotar el scraping).

**⚠️ CRITICAL**: US1 y US2 no pueden completarse sin esta fase.

- [X] T003 Migración `src/MPM.Api/Database/Scripts/V118__Create_Areas_Negocio.sql`: `CREATE TABLE areas_negocio (codigo SMALLINT PK, nombre VARCHAR(50), palabras_clave TEXT[], created_at, updated_at)` + `INSERT` semilla de las 3 áreas (Cloud, Ciberseguridad, Digital) con listas de palabras clave curadas en español — ver `data-model.md`
- [X] T004 En la misma migración V118: función SQL `fn_licitacion_area_codigos(p_search_vector tsvector) RETURNS SMALLINT[]` que devuelve los códigos de `areas_negocio` cuyo `palabras_clave` matchea contra `p_search_vector` vía `plainto_tsquery('spanish', kw)` (ver `research.md` §1) — función compartida para no duplicar la lógica de matching entre `usp_Licitaciones_Listar` (US1) y `usp_Licitaciones_ContarPorEstado` (US2)
- [X] T005 `usp_Catalogos_AreasNegocio()` (misma migración V118): `SELECT codigo, nombre FROM areas_negocio ORDER BY codigo`
- [X] T006 [P] Agregar constante `AreasNegocio` en `src/MPM.Modules.Catalogo/Data/CatalogoStoredProcedures.cs` y endpoint `GET api/v1/catalogos/areas-negocio` en `src/MPM.Modules.Catalogo/Controllers/CatalogoController.cs` (ver `contracts/licitaciones-area-y-estadisticas.md`)
- [X] T007 [P] Frontend: hook `useAreasNegocio` en `src/mpm-web/src/hooks/useCatalogos.ts` (o el archivo de hooks de catálogo existente) consumiendo el endpoint de T006

**Checkpoint**: catálogo de áreas de negocio disponible y consultable; base lista para US1 y US2.

---

## Phase 3: User Story 1 - Filtro de licitaciones por área de negocio (Priority: P1) 🎯 MVP

**Goal**: filtrar el listado de licitaciones por Cloud/Ciberseguridad/Digital en vez de ver los 183.000+ registros completos, con una vía de acceso a las no clasificadas.

**Independent Test**: `GET /api/v1/licitaciones?area=2` devuelve un subconjunto acotado y relevante; `GET /api/v1/licitaciones?sinClasificar=true` sigue devolviendo licitaciones (Escenario US1 de `quickstart.md`).

### Implementation for User Story 1

- [X] T008 [US1] Migración `src/MPM.Api/Database/Scripts/V119__Licitaciones_Filtro_Area_Y_Stats_Estado.sql`: reescribir `usp_Licitaciones_Listar` (base actual en `V093`) agregando `p_area SMALLINT DEFAULT NULL`, `p_sin_clasificar BOOLEAN DEFAULT NULL`; cláusula `WHERE (p_area IS NULL OR p_area = ANY(fn_licitacion_area_codigos(l.search_vector))) AND (p_sin_clasificar IS NOT TRUE OR fn_licitacion_area_codigos(l.search_vector) = '{}')` (ver `contracts/licitaciones-area-y-estadisticas.md` — `area` tiene prioridad si vienen ambos parámetros)
- [X] T009 [US1] Actualizar la constante `Listar` en `src/MPM.Modules.Licitaciones/Data/LicitacionStoredProcedures.cs` con los 2 parámetros nuevos
- [X] T010 [US1] Agregar `Area` (`short?`) y `SinClasificar` (`bool?`) a `src/MPM.Modules.Licitaciones/Models/LicitacionFilter.cs`
- [X] T011 [US1] Pasar los filtros nuevos en `src/MPM.Modules.Licitaciones/Data/LicitacionHandler.cs` (método `ListarAsync`) y `src/MPM.Modules.Licitaciones/Services/LicitacionService.cs`
- [X] T012 [US1] Agregar query params `area`/`sinClasificar` en `LicitacionController.Listar` (`src/MPM.Modules.Licitaciones/Controllers/LicitacionController.cs`)
- [X] T013 [P] [US1] Unit test en `tests/MPM.Modules.Licitaciones.Tests/Services/LicitacionServiceTests.cs`: `area` y `sinClasificar` llegan correctamente al handler; `area` tiene prioridad sobre `sinClasificar` si vienen ambos
- [X] T014 [US1] Frontend: agregar `area`/`sinClasificar` a `src/mpm-web/src/hooks/useLicitaciones.ts`
- [X] T015 [US1] Frontend: selector de área de negocio (consumiendo `useAreasNegocio` de T007) + toggle "sin clasificar" en `src/mpm-web/src/components/LicitacionFilterBar.tsx`
- [X] T016 [US1] Frontend: conectar el selector nuevo en `src/mpm-web/src/pages/LicitacionesPage.tsx`, confirmar que el conteo total de resultados baja visiblemente al aplicar un área (SC-001)

**Checkpoint**: US1 funcional de punta a punta — filtro por área operativo en la UI, sin perder acceso a licitaciones sin clasificar.

---

## Phase 4: User Story 3 - Orden del historial de análisis por fecha de adjudicación (Priority: P1)

**Goal**: el listado de análisis se ordena por la fecha de adjudicación de la licitación, no por cuándo se generó el análisis.

**Independent Test**: un análisis de una licitación adjudicada la semana pasada aparece antes que uno de febrero, sin importar cuál se generó primero en el sistema (Escenario US3 de `quickstart.md`).

### Implementation for User Story 3

- [X] T017 [US3] Migración `src/MPM.Api/Database/Scripts/V120__AnalisisWorkspaces_Orden_Fecha_Adjudicacion.sql`: reescribir `usp_AnalisisWorkspaces_Listar` (base actual en `V113`) para proyectar `l.fecha_adjudicacion` y cambiar el `ORDER BY` a `COALESCE(l.fecha_adjudicacion, l.fecha_estimada_adjudicacion) DESC NULLS LAST, aw.created_at DESC` (ver `contracts/analisis-orden.md` — cubre el edge case de licitaciones sin fecha de adjudicación registrada)
- [X] T018 [US3] Agregar `FechaAdjudicacion` (nullable) al DTO de item de lista en `src/MPM.Modules.Analisis/Models/` (el DTO que devuelve `AnalisisController.ListarWorkspaces`) y mapear el campo nuevo en `src/MPM.Modules.Analisis/Data/AnalisisHandler.cs`
- [X] T019 [P] [US3] Unit/integration test confirmando el nuevo orden en `tests/MPM.Modules.Analisis.Tests/` (dos workspaces con fechas de adjudicación cruzadas respecto a su `created_at`, confirmar orden esperado; caso adicional con `fecha_adjudicacion IS NULL`)
- [X] T020 [US3] Frontend: mostrar `fechaAdjudicacion` como badge/subtítulo en la tarjeta de cada análisis en `src/mpm-web/src/pages/AnalisisListPage.tsx` (evita repetir la confusión original: el usuario ahora ve por qué algo aparece primero)

**Checkpoint**: US1 y US3 (ambas P1) funcionando de forma independiente — el MVP de esta spec.

---

## Phase 5: User Story 2 - Estadísticas de licitaciones por estado con drill-down (Priority: P2)

**Goal**: desglose numérico por estado (publicada/cerrada/desierta/adjudicada/revocada), navegable hacia el listado filtrado detrás de cada número.

**Independent Test**: `GET /api/v1/licitaciones/estadisticas-estado` devuelve los 5 estados reales con conteo; hacer clic en "desiertas" navega a `GET /api/v1/licitaciones?estado=7` con el mismo conteo (Escenario US2 de `quickstart.md`).

### Implementation for User Story 2

- [X] T021 [US2] Migración `src/MPM.Api/Database/Scripts/V121__Licitaciones_Contar_Por_Estado.sql`: nuevo SP `usp_Licitaciones_ContarPorEstado(p_area SMALLINT DEFAULT NULL, p_sin_clasificar BOOLEAN DEFAULT NULL)`, `LEFT JOIN estados_licitacion → licitaciones` (para incluir estados con conteo 0) reutilizando `fn_licitacion_area_codigos` de T004
- [X] T022 [US2] Agregar la constante `ContarPorEstado` en `LicitacionStoredProcedures.cs`, método `ContarPorEstadoAsync` en `LicitacionHandler.cs`/`LicitacionService.cs`, endpoint `GET api/v1/licitaciones/estadisticas-estado` en `LicitacionController.cs` (mismos archivos tocados en Fase 3 — coordinar si se ejecuta en paralelo con Fase 3)
- [X] T023 [P] [US2] Unit test en `tests/MPM.Modules.Licitaciones.Tests/Services/LicitacionServiceTests.cs`: los 5 estados reales aparecen siempre (incluso con `cantidad: 0`), y el filtro `area` reduce los conteos consistentemente
- [X] T024 [US2] Frontend: nuevo componente de estadísticas por estado (tarjetas o barra clicable) en `src/mpm-web/src/pages/LicitacionesPage.tsx` o una sección propia, navegando a `useLicitaciones` con `estado`+`area` al hacer clic (drill-down, sin endpoint ni vista adicional)

**Checkpoint**: US1, US3 y US2 funcionando juntas.

---

## Phase 6: User Story 5 - Flujo colaborativo go/no-go (Priority: P2)

**Goal**: marcar una licitación de interés, disparar un único análisis bajo demanda (reutilizado si ya existe), asignar a trabajadores y dejar comentarios internos visibles entre ellos.

**Independent Test**: `POST /interes` dos veces sobre la misma licitación no crea una segunda fila ni un segundo análisis; dos usuarios asignados a la misma conversación ven los comentarios del otro (Escenario US5 de `quickstart.md`).

### Implementation for User Story 5

- [X] T025 [US5] Scaffold del módulo nuevo `MPM.Modules.Colaboracion` (usar el skill `new-module` de este repo si está disponible, o replicar la estructura estándar `Controllers/Services/Data/Models/ModuleRegistration.cs` de un módulo chico existente) + registrar `AddColaboracionModule()` en `src/MPM.Api/Program.cs`
- [X] T026 [US5] Migración `src/MPM.Api/Database/Scripts/V122__Create_Licitaciones_Interes.sql`: `licitaciones_interes(id, licitacion_id BIGINT UNIQUE REFERENCES licitaciones(id), workspace_id BIGINT NULL REFERENCES analisis_workspaces(id), conversacion_id BIGINT NULL REFERENCES conversaciones(id), marcado_por VARCHAR(100), estado_licitacion_al_marcar SMALLINT, created_at, updated_at)` + SPs `usp_LicitacionesInteres_Marcar` (idempotente: `INSERT ... ON CONFLICT (licitacion_id) DO NOTHING` + `SELECT`), `usp_LicitacionesInteres_ObtenerPorLicitacion`, `usp_LicitacionesInteres_VincularWorkspace`, `usp_LicitacionesInteres_VincularConversacion`, `usp_LicitacionesInteres_Listar` (ver `data-model.md`)
- [X] T027 [US5] **Corrección necesaria en Analisis** (fuera de `Colaboracion`, pero bloqueante para que US5 cumpla FR-013): `usp_AnalisisWorkspaces_Crear` (`V052`) hoy siempre hace `INSERT` sin revisar si ya existe un workspace para ese `licitacion_id` — agregar una migración `src/MPM.Api/Database/Scripts/V123__AnalisisWorkspaces_Crear_Idempotente.sql` que, si ya existe un workspace no eliminado para ese `licitacion_id`, lo devuelva en vez de insertar uno nuevo (confirmar en `tests/MPM.Modules.Analisis.Tests/` que un segundo `POST /workspaces` sobre la misma licitación no duplica)
- [X] T028 [US5] `Data/LicitacionesInteresStoredProcedures.cs` + `Data/LicitacionesInteresHandler.cs` en `src/MPM.Modules.Colaboracion/`
- [X] T029 [US5] `Services/LicitacionesInteresService.cs`: `MarcarInteresAsync`, `VincularWorkspaceAsync`, `VincularConversacionAsync`, `ObtenerAsync`, `ListarAsync` en `src/MPM.Modules.Colaboracion/`
- [X] T030 [US5] `Controllers/LicitacionesInteresController.cs`: `POST api/v1/licitaciones/{licitacionId}/interes`, `PATCH api/v1/licitaciones/{licitacionId}/interes/vincular`, `GET api/v1/licitaciones/{licitacionId}/interes`, `GET api/v1/licitaciones/interes` (ver `contracts/colaboracion-interes.md`)
- [X] T031 [P] [US5] Crear `tests/MPM.Modules.Colaboracion.Tests/MPM.Modules.Colaboracion.Tests.csproj` (copiar la estructura de `tests/MPM.Modules.Competidores.Tests/` — 3 `ProjectReference`: el módulo nuevo, `MPM.Core`, `MPM.Shared`) + unit tests de `LicitacionesInteresService` (idempotencia de `MarcarInteresAsync`, detección de cambio de estado FR-017)
- [X] T032 [US5] Frontend: hook `useLicitacionesInteres.ts` en `src/mpm-web/src/hooks/` que orquesta los 3 pasos del contrato: `POST /interes` → `POST /api/v1/analisis/workspaces` (existente) → `POST /api/v1/conversaciones` con `licitacionId` (existente, ya soporta el campo) → `PATCH /interes/vincular`
- [X] T033 [US5] Frontend: componente `src/mpm-web/src/components/LicitacionInteresPanel.tsx` — botón "Marcar de interés", estado de carga durante el análisis, lista de participantes asignados (reusa `POST /conversaciones/{id}/participantes` existente), hilo de comentarios (reusa `POST /conversaciones/{id}/mensajes` + SignalR existente)
- [X] T034 [US5] Frontend: aviso visual cuando `estadoLicitacionAlMarcar` difiere del estado actual de la licitación (FR-017), dentro del mismo panel de T033
- [X] T035 [US5] Playwright E2E en `src/mpm-web/e2e/`: flujo completo marcar interés → análisis generado → asignar 2 usuarios → cada uno comenta → ambos ven el comentario del otro

**Checkpoint**: US1, US3, US2 y US5 funcionando juntas — todo el bloque P1+P2 completo.

---

## Phase 7: User Story 4 - Informe ejecutivo de competidores con actividad total de mercado (Priority: P3)

**Goal**: para un competidor ya identificado, mostrar además de los encuentros directos su actividad total de mercado (licitaciones + montos adjudicados) incluyendo licitaciones donde TIVIT no participó.

**Independent Test**: `GET /{nombre}/actividad-mercado` responde `202` la primera vez, y tras el scraping en background, `200` con al menos una licitación en `tivitParticipo: false` (Escenario US4 de `quickstart.md`).

**⚠️ Riesgo no resuelto** (ver `research.md` §4): el volumen real de licitaciones a scrapear por competidor+área+período no se conoce hasta correrlo contra datos reales — validar con T041 antes de considerar esta historia cerrada.

### Implementation for User Story 4

- [X] T036 [US4] Migración `src/MPM.Api/Database/Scripts/V124__Create_Competidores_Actividad_Mercado.sql`: tabla `competidores_actividad_mercado` (cache, misma clave que `competidores_analisis` de `V098` + `area_codigo`) + SPs `usp_CompetidoresActividadMercado_ObtenerCache`, `_Crear` (estado `generando`), `_Guardar` (ver `data-model.md`)
- [X] T037 [US4] `tools/scraper-mp-v2/modulos/buscarPublico.js`: nuevo, basado en la estructura de `buscarLicitaciones` (`modulos/buscar.js`) pero sin `seleccionarFiltroOfertado` — usa el radio/modo confirmado en T002, acotado a estado∈{adjudicada,cerrada,desierta}, rango de fechas y término de búsqueda derivado del área (palabras clave de `areas_negocio`, mismas que T003); reutiliza `extraerCuadroOfertas` de `modulos/cuadroOfertas.js` sin modificarlo. Requiere exportar (o duplicar) los helpers privados `esperarPostbackLibre`/`ejecutarBusqueda` de `buscar.js`
- [X] T038 [US4] `src/MPM.Modules.Competidores/Data/CompetidoresActividadMercadoHandler.cs` + `CompetidoresStoredProcedures.cs` (agregar las constantes de T036)
- [X] T039 [US4] `src/MPM.Modules.Competidores/Services/CompetidorMercadoService.cs`: `ObtenerOGenerarActividadMercadoAsync`, replicando el patrón get-or-generate de `CompetidorAnalysisService.ObtenerOGenerarAnalisisAsync` (cache-hit → devuelve; cache-miss → crea fila `generando`, encola el scraping en background, responde `202`) — confirmar en esta tarea el mecanismo real de "encolar en background" usado hoy por `ScraperBackgroundService`/Cloud Run Jobs y replicarlo, no inventar uno nuevo
- [X] T040 [US4] Endpoint `GET api/v1/competidores/{nombre}/actividad-mercado` en `src/MPM.Modules.Competidores/Controllers/CompetidoresController.cs` (ver `contracts/competidores-actividad-mercado.md`)
- [X] T041 [US4] Corrida real contra producción/staging (mismo criterio de verificación que el resto del proyecto — no confiar en el diseño sin probarlo): medir cuántas licitaciones toca `buscarPublico.js` para un área+período reales y cuánto tarda; si el volumen es inmanejable, documentar la mitigación de reserva (acotar también por región/organismo) en `research.md` antes de continuar
- [X] T042 [P] [US4] Unit tests de `CompetidorMercadoService` en `tests/MPM.Modules.Competidores.Tests/` (cache-hit, cache-miss dispara encolado, no vuelve a encolar si ya está `generando`) — mockeando el disparador de scraping, sin red real
- [X] T043 [US4] Frontend: extender `src/mpm-web/src/hooks/useCompetidores.ts` con `useActividadMercado` (polling mientras `estado: 'generando'`)
- [X] T044 [US4] Frontend: panel "Actividad total de mercado" en `src/mpm-web/src/pages/CompetidoresPage.tsx`, distinguiendo visualmente `tivitParticipo: true/false` dentro de la misma lista (brecha de mercado vs. encuentro directo)

**Checkpoint**: las 5 historias de usuario funcionando de forma independiente y en conjunto.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T045 `dotnet build MPM.sln` y `dotnet test` completo — cero regresiones en todos los módulos, incluyendo `MPM.Modules.Colaboracion` nuevo
- [X] T046 Ejecutar `quickstart.md` completo contra `docker compose` local (6 escenarios, uno por historia + el de estadísticas)
- [X] T047 [P] Confirmar que `CHANGELOG.md` refleja el nuevo módulo `Colaboracion` y las 7 migraciones (V118-V124, sujeto a la numeración real confirmada en T001)
- [X] T048 Actualizar `specs/ROADMAP.md`: mover `031-feedback-chilecompra` de "Prioridad 0 — nueva" a su estado real post-implementación

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias — arranca de inmediato
- **Foundational (Phase 2)**: depende de T001 (Setup) — bloquea US1 y US2 (no bloquea US3, US4 ni US5, que no dependen de áreas de negocio)
- **US1 (Phase 3)** y **US3 (Phase 4)**: ambas P1, ambas pueden arrancar en paralelo tras su respectiva fase bloqueante (US1 tras Foundational; US3 no depende de Foundational en absoluto, puede arrancar apenas termine Setup)
- **US2 (Phase 5)**: depende de Foundational (T004) y comparte archivos con US1 (T009/T011/T012 tocan los mismos 3 archivos que T022) — recomendado secuencial después de US1, no en paralelo, para evitar conflictos de merge
- **US5 (Phase 6)**: depende de Foundational solo indirectamente (ninguna dependencia real de áreas de negocio) — puede arrancar en paralelo con US1/US2/US3 en cuanto Setup termine
- **US4 (Phase 7)**: depende de Foundational (reutiliza `areas_negocio`/T003 para acotar el scraping) y de T002 (Setup, investigación en vivo) — es la única historia con una dependencia externa real (el scraper) y el mayor riesgo, por eso queda última pese a no depender técnicamente de US1/US2/US3/US5
- **Polish (Phase 8)**: depende de todas las historias que se decida incluir en el release

### Parallel Opportunities

- T002 (Setup) es paralelo a T001
- Dentro de Foundational: T006/T007 son paralelos entre sí (una vez completos T003-T005)
- US1 y US3 (ambas P1) son paralelizables entre equipos distintos
- US5 es paralelizable con US1/US2/US3 (módulo nuevo, sin archivos compartidos)
- US4 puede empezar su parte de scraper (T037, tras T002) en paralelo con el resto — el resto de US4 (backend/frontend) depende de esa pieza

---

## Implementation Strategy

### MVP First (US1 + US3, ambas P1)

1. Completar Fase 1: Setup
2. Completar Fase 2: Foundational (bloquea US1, no bloquea US3)
3. Completar Fase 3 (US1) y Fase 4 (US3) — en paralelo si hay capacidad
4. **STOP y VALIDAR**: correr los escenarios US1/US3 de `quickstart.md`
5. Deploy/demo — ya resuelve las dos quejas de usabilidad más citadas en la reunión

### Entrega incremental

1. Setup + Foundational → base lista
2. US1 + US3 → MVP, demo
3. US2 → estadísticas con drill-down, demo
4. US5 → flujo colaborativo completo, demo
5. US4 → informe de competidores ampliado (la más incierta, dejar al final a propósito)

### Nota sobre US4

A diferencia de las otras 4 historias, US4 no está lista para estimar con confianza — su primera tarea real de implementación (T037, el scraper nuevo) es también la que valida si el diseño de `research.md` §4 es viable tal cual, o si necesita la mitigación de reserva (acotar más el volumen). Tratarla como un spike interno antes de comprometer una fecha de entrega específica para esta historia.
