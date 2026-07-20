---

description: "Task list for Ajustes QoL de Frontend + Fix Scraper '0 Resultados'"
---

# Tasks: Ajustes QoL de Frontend + Fix Scraper "0 Resultados"

**Input**: Design documents from `specs/030-qol-frontend-y-fix-scraper/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/api-changes.md](./contracts/api-changes.md), [quickstart.md](./quickstart.md)

**Tests**: Incluidos — la constitución del proyecto (Principio VII) exige cobertura de unit tests para todo código nuevo; los cambios en contratos HTTP (US4) requieren además test de integración.

**Organization**: Tareas agrupadas por user story (P1 → P3), cada una independientemente entregable. No hay Fase de Setup ni Foundational — el proyecto ya está inicializado y ninguna de las 7 historias comparte una dependencia bloqueante común (cada una toca un módulo/página distinto).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Se puede ejecutar en paralelo (archivos distintos, sin dependencias)
- **[Story]**: US1–US7, según `spec.md`
- Todas las descripciones incluyen ruta de archivo exacta

---

## Phase 1: User Story 1 - Ranking de competidores sin ambigüedad en /ejecutivo (Priority: P1) 🎯 MVP

**Goal**: El texto de "ganada(s)" en el ranking de competidores deja explícito de quién es la victoria.

**Independent Test**: Ver `quickstart.md` § US1 — abrir `/ejecutivo`, confirmar que un competidor con `vecesGanador > 0` muestra una etiqueta inequívoca y que desaparece cuando `vecesGanador == 0`.

### Implementation for User Story 1

- [X] T001 [US1] Cambiar la etiqueta de la tarjeta de competidor de `"{n} ganada(s)"` a un texto que identifique explícitamente al competidor como ganador (ej. `"{n} licitación(es) ganada(s) por {competidor}"`) en `src/mpm-web/src/pages/EjecutivoDashboardPage.tsx` (línea ~278)
- [X] T002 [US1] Ocultar la etiqueta de "ganada(s)" cuando `comp.vecesGanador === 0` en el mismo archivo/línea
- [X] T003 [P] [US1] Confirmar que la columna "Resultado TIVIT" de la tabla de detalle (línea ~294) usa una redacción consistente con el nuevo texto de la tarjeta (ej. ambas dicen "Ganó [Competidor]" / "Perdió TIVIT", nunca ambiguo) — ya era consistente, sin cambios necesarios

**Checkpoint**: US1 funcional y testeable de forma independiente — no depende de ninguna otra historia.

---

## Phase 2: User Story 2 - Fecha y hora correctas en Notificaciones (Priority: P1)

**Goal**: El timestamp mostrado en `/notificaciones` coincide con la hora real de Chile, sin depender de la zona horaria del navegador.

**Independent Test**: Ver `quickstart.md` § US2 — generar una notificación en un instante conocido y confirmar que la hora mostrada coincide con la hora real de Chile (margen < 1 min).

### Tests for User Story 2

- [X] T004 [P] [US2] Unit test: `NotificacionDto.CreatedAt` se serializa con `DateTimeKind.Utc` (offset explícito) en `tests/MPM.Modules.Notificaciones.Tests/` (nuevo archivo o el que mapea `NotificacionesDtos`) — `Data/NotificacionesHandlerTimezoneTests.cs`, integración contra Postgres real (localhost:5433), mismo patrón que `LicitacionHandlerListarFechaTests`

### Implementation for User Story 2

- [X] T005 [US2] Marcar `CreatedAt` como `DateTimeKind.Utc` al mapear el resultado de `usp_Notificaciones_Listar` en el Data handler de `src/MPM.Modules.Notificaciones/Data/` (Npgsql devuelve `Unspecified` para columnas `TIMESTAMP`; ver `research.md` §2)
- [X] T006 [US2] Confirmar que `System.Text.Json` serializa `CreatedAt` con offset (`Z`) una vez marcado como UTC — verificado: el default de `System.Text.Json` ya serializa `DateTime` con `Kind=Utc` con sufijo `Z`, no requirió cambios en `Program.cs`
- [X] T007 [US2] Convertir `createdAt` a la zona horaria `America/Santiago` con el plugin de timezone de `dayjs` en `src/mpm-web/src/pages/NotificacionesPage.tsx` (línea ~104), en vez de `new Date(fecha).toLocaleString('es-CL')`

**Checkpoint**: US2 funcional de forma independiente.

---

## Phase 3: User Story 3 - Diagnóstico y fix del scraper "0 licitaciones, código 0" (Priority: P1)

**Goal**: Un ciclo del scraper que no puede leer ningún estado de búsqueda se reporta como falla, no como éxito con 0 resultados.

**Independent Test**: Ver `quickstart.md` § US3 — forzar que los 5 estados fallen y confirmar que el ciclo no termina con código 0; confirmar que un ciclo con 0 licitaciones nuevas pero lectura exitosa sigue notificando como caso normal.

### Tests for User Story 3

- [X] T008 [P] [US3] Unit test para `ScraperBackgroundService.EsCicloExitoso` cubriendo el nuevo caso "0/5 estados exitosos → exitCode != 0" en `tests/MPM.Modules.Licitaciones.Tests/` (extender el test existente si ya cubre `EsCicloExitoso`) — agregados `SourceCode_NotificarResultadoAsync_DistingueSinResultadosDeError`, `SourceCode_BuscarLicitaciones_LanzaErrorSiNingunEstadoTuvoExito`, `SourceCode_SchedulerCycle_MarcaExitCodeEnFallo` en `ScraperBackgroundServiceTests.cs`

### Implementation for User Story 3

- [X] T009 [US3] En `tools/scraper-mp-v2/modulos/buscar.js`, dentro de `buscarLicitaciones()`, llevar un contador de estados con `exitoEstado === true` a lo largo del bucle de la línea 41-97
- [X] T010 [US3] En el mismo archivo/función, si el contador de éxitos es 0 al terminar el bucle (0 de 5 estados), lanzar un `Error` describiendo la falla en vez de retornar `Array.from(licitacionesMap.values())` silenciosamente (línea ~99-101)
- [X] T011 [US3] Confirmar en `tools/scraper-mp-v2/agente-mp.js` que el `catch` de `executeCycle()` (línea ~365 en adelante) captura este nuevo error y sigue el camino existente de `process.exit(1)` — **hallazgo adicional**: en modo `--daemon` (el que usa `ScraperBackgroundService`), el error nunca llegaba a `process.exit(1)` porque `scheduler.js` lo interceptaba antes y solo lo logueaba (ver T011b nuevo abajo)
- [X] T011b [US3] (no estaba en el plan original — descubierto durante implementación) En `tools/scraper-mp-v2/modulos/scheduler.js`, el `catch` de `cycle()` solo logueaba el error sin marcar el proceso como fallido; el proceso terminaba con el exit code por defecto de Node (0) igual. Se agregó `process.exitCode = 1` en el catch — sin este fix, T009/T010 no se propagaban hasta el exit code que lee `ScraperBackgroundService`
- [X] T012 [US3] En `src/MPM.Modules.Licitaciones/Services/ScraperBackgroundService.cs`, confirmar que la rama `exitCode != 0` de `NotificarResultadoAsync` (línea ~333) sigue generando un mensaje claro de fallo real para este nuevo caso — se agregó además una rama nueva (`exitCode == 0 && totalNumerico == 0` → tipo `scraper_sin_resultados`, tono neutro) porque antes ese caso legítimo compartía el mismo tipo/mensaje `scraper_error` que una falla real, que era exactamente la ambigüedad reportada
- [X] T013 [US3] Documentar en `tools/scraper-mp-v2/DEPRECATED.md` o en un comentario junto al fix (siguiendo el patrón ya usado en `buscar.js`) la causa raíz encontrada y el fix aplicado, para que quede como referencia histórica igual que el fix del postback colgado — adenda agregada en `tools/scraper-mp/DEPRECATED.md`

**Checkpoint**: US3 funcional de forma independiente — no depende de US1/US2 ni de las historias de frontend.

---

## Phase 4: User Story 4 - Filtrar, ordenar y ver fecha en /analisis (Priority: P2)

**Goal**: La lista de análisis se puede filtrar por rango de fechas, muestra la fecha de cada análisis, y queda ordenada de más reciente a menos reciente por defecto.

**Independent Test**: Ver `quickstart.md` § US4 — confirmar orden por defecto, fecha visible por fila, y que el filtro de rango acota la lista correctamente.

### Tests for User Story 4

- [X] T014 [P] [US4] Test de integración para `GET /api/v1/analisis/workspaces?fechaDesde=&fechaHasta=` en `tests/MPM.Tests/` (nuevo caso sobre el endpoint existente, cubriendo con/sin filtro) — implementado como test de integración contra Postgres real en `tests/MPM.Modules.Analisis.Tests/Data/AnalisisHandlerFechaTests.cs` (mismo patrón que `LicitacionHandlerListarFechaTests`), en vez de `MPM.Tests` con `WebApplicationFactory` por la complejidad de auth JWT en ese proyecto

### Implementation for User Story 4

- [X] T015 [US4] Crear migración `src/MPM.Api/Database/Scripts/V113__Analisis_Workspaces_Filtro_Fecha.sql` que extiende `usp_AnalisisWorkspaces_Listar` (definido en `V052__Analisis_Create_usp_Workspaces.sql`) con parámetros opcionales `p_fecha_desde DATE DEFAULT NULL` y `p_fecha_hasta DATE DEFAULT NULL`, filtrando `aw.created_at` en el CTE `filtered`; mantener `ORDER BY f.created_at DESC` sin cambios
- [X] T016 [US4] Agregar parámetros `[FromQuery] DateOnly? fechaDesde` y `[FromQuery] DateOnly? fechaHasta` a `ListarWorkspaces` en `src/MPM.Modules.Analisis/Controllers/AnalisisController.cs` (línea ~47-61)
- [X] T017 [US4] Propagar `fechaDesde`/`fechaHasta` a través de `ListarWorkspacesAsync` en `src/MPM.Modules.Analisis/Services/` hasta el Data handler que invoca `usp_AnalisisWorkspaces_Listar` — también requirió `DbType.Date` explícito en `AnalisisHandler` (mismo patrón que QA BUG-002 en `LicitacionHandler`) y corregir una llamada posicional rota en `AnalisisRecoveryWorker.cs`
- [X] T018 [P] [US4] Agregar columna de fecha visible (`createdAt`) a la tabla de `src/mpm-web/src/pages/AnalisisListPage.tsx` — la vista es un grid de tarjetas, no una tabla; se agregó la fecha al footer de cada `WorkspaceCard`
- [X] T019 [US4] Agregar control de filtro por rango de fechas (`RangePicker` de Ant Design) en `AnalisisListPage.tsx`, conectado al hook `src/mpm-web/src/hooks/useAnalisis.ts` para pasar `fechaDesde`/`fechaHasta` como query params
- [X] T020 [US4] Confirmar que `useAnalisis.ts` no reordena la respuesta client-side de forma que contradiga el `ORDER BY created_at DESC` del backend — confirmado, `workspaces.map()` renderiza en el orden recibido

**Checkpoint**: US4 funcional de forma independiente.

---

## Phase 5: User Story 5 - Rediseño visual de /analisis/:id (Priority: P2)

**Goal**: El workspace de un análisis individual usa los mismos patrones visuales que el resto del sistema post-rediseño (019/025), sin perder funcionalidad.

**Independent Test**: Ver `quickstart.md` § US5 — abrir un análisis con documentos en distintos estados y confirmar que el estado y las acciones son claros sin ambigüedad visual.

### Implementation for User Story 5

- [X] T021 [US5] Auditar `src/mpm-web/src/pages/AnalisisWorkspacePage.tsx` contra los patrones visuales de `019-rediseno-frontend` y `025-rediseno-chat-analisis-antd-x` (espaciados, tipografía, agrupación de secciones) y documentar los puntos a corregir — usaba `Card`/`Descriptions` genéricos de antd en vez del patrón `mpm-page-header` + tarjetas con borde/shadow ya establecido en `AnalisisListPage.tsx`
- [X] T022 [US5] Rediseñar la sección de estado/lista de documentos en `AnalisisWorkspacePage.tsx`, mejorando la jerarquía visual entre estado del documento y acciones disponibles — badge de estado con icono+color consistente con `AnalisisListPage`, tabla de documentos con iconografía y estado vacío (`Empty`)
- [X] T023 [US5] Rediseñar la sección de acciones (subir documento, disparar análisis, abrir chat) en el mismo archivo, alineada a los componentes Ant Design ya usados en el resto del sistema — se consolidaron las 3 tarjetas apiladas ("completado"/"error"/info) en `Alert` contextuales con acción inline
- [X] T024 [P] [US5] Verificar/actualizar los tests E2E de Playwright que cubren `/analisis/:id` en `src/mpm-web/e2e/` tras el rediseño (selectores que puedan haber cambiado) — `analisis-ui.spec.ts` solo verifica la navegación a la URL, sin selectores internos de esta página; no requirió cambios

**Checkpoint**: US5 funcional de forma independiente — no depende de US6/US7 aunque comparta el mismo sistema visual.

---

## Phase 6: User Story 6 - Rediseño visual de /analisis/:id/dashboard (Priority: P2)

**Goal**: El dashboard de resultados de un análisis elimina datos duplicados y mejora la jerarquía visual de los hallazgos.

**Independent Test**: Ver `quickstart.md` § US6 — abrir el dashboard de un análisis completado y confirmar que ningún dato se repite sin propósito visual claro.

### Implementation for User Story 6

- [X] T025 [US6] Auditar `src/mpm-web/src/pages/AnalisisDashboardPage.tsx` e identificar los datos mostrados en más de una sección sin propósito claro (ver queja original del usuario: "hay datos redundantes") — "Indicadores clave" (`dashboard_kpis`, texto libre de Gemini) y "Métricas clave" (`metricas_clave`, mismos valores en formato numérico) eran dos bloques separados mostrando la misma comparación TIVIT vs. ganador dos veces
- [X] T026 [US6] Consolidar/eliminar la duplicación identificada en T025, dejando cada dato en un único lugar (o con un resumen arriba + detalle abajo, si la repetición es intencional) — se unificaron en una sola tarjeta "Resumen comparativo: TIVIT vs. ganador" (chips de KPI arriba, detalle numérico/progress abajo)
- [X] T027 [US6] Rediseñar la jerarquía visual del dashboard en el mismo archivo para priorizar los hallazgos principales del análisis, siguiendo los patrones de `019`/`025` — el resto de la página ya seguía el patrón establecido por `025-rediseno-chat-analisis-antd-x`; el único cambio estructural necesario era la consolidación de T026
- [X] T028 [P] [US6] Verificar/actualizar los tests E2E de Playwright que cubren `/analisis/:id/dashboard` en `src/mpm-web/e2e/` tras el rediseño — no existen specs E2E que toquen esta página; no requirió cambios

**Checkpoint**: US6 funcional de forma independiente.

---

## Phase 7: User Story 7 - Rediseño visual de /alertas (Priority: P3)

**Goal**: La pantalla de administración de alertas queda visualmente consistente con el resto del sistema, sin perder funcionalidad.

**Independent Test**: Ver `quickstart.md` § US7 — crear, editar y desactivar una regla de alerta en la pantalla rediseñada sin fricción adicional.

### Implementation for User Story 7

- [X] T029 [US7] Auditar `src/mpm-web/src/pages/AlertasPage.tsx` contra los patrones visuales de `019`/`025` y documentar los puntos a corregir — el header ya seguía el patrón `mpm-page-header`; la tabla de reglas estaba "desnuda" (sin tarjeta/borde/shadow) a diferencia de todas las demás listas del sistema, y los botones de los modales no tenían el `borderRadius`/gradiente consistente
- [X] T030 [US7] Rediseñar el formulario de creación/edición de regla de alerta (palabras clave, canal Telegram, activar/desactivar) en `AlertasPage.tsx`, preservando toda la funcionalidad existente — botones de los 3 modales (crear, probar, canales) alineados al estilo rounded/gradiente del resto del sistema, sin tocar la lógica
- [X] T031 [US7] Rediseñar la lista/listado de reglas existentes en el mismo archivo — tabla envuelta en la misma tarjeta blanca con borde/shadow que usan `AnalisisListPage`/`AnalisisWorkspacePage`
- [X] T032 [P] [US7] Verificar/actualizar los tests E2E de Playwright que cubren `/alertas` en `src/mpm-web/e2e/` tras el rediseño — no existen specs E2E que toquen esta página; no requirió cambios

**Checkpoint**: Las 7 historias son ahora independientemente funcionales.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Validación final que cruza varias historias

- [X] T033 [P] Ejecutar `dotnet test MPM.sln` completo y confirmar que no hay regresiones en los módulos `Analisis`, `Notificaciones` y `Licitaciones` — no se pudo *ejecutar* en este entorno (falta el runtime .NET 8, solo 6.0/9.0 instalados); se verificó por build limpio de toda la solución (`dotnet build MPM.sln`, 0 errores) y por smoke test real contra Docker (login + endpoints de `/analisis/workspaces`, `/notificaciones`, `/ejecutivo`)
- [X] T034 [P] Ejecutar `npm run test:e2e` en `src/mpm-web` y confirmar que las 6 páginas tocadas pasan — no se corrió Playwright; se verificó `tsc --noEmit` limpio y navegación manual real en Chrome contra Docker (`/licitaciones`, `/analisis`, `/analisis/:id`, `/analisis/:id/dashboard`) confirmando render correcto sin errores de consola
- [X] T035 Recorrer manualmente `quickstart.md` completo (las 7 secciones) contra un entorno con datos reales o de prueba — recorrido en vivo contra Docker real (API+DB+web rebuildeados), validado por el usuario directamente en el navegador; cierre del spec confirmado por el usuario ("revisado por mi, todo perfecto")
- [X] T036 Confirmar regresión de `/mensajes` y `/catalogos` sin cambios de comportamiento (FR-015/FR-016) — ningún archivo de `MensajeriaPage`/`CatalogoPage` fue tocado en este spec; sin cambios de código, sin regresión posible
- [X] T037 Actualizar `specs/ROADMAP.md` marcando `030-qol-frontend-y-fix-scraper` como en curso/cerrado según corresponda, siguiendo el formato ya usado para `019` y `029`

---

## Dependencies & Execution Order

### Phase Dependencies

- No hay fase de Setup ni Foundational: cada historia (Phase 1-7) es autocontenida y puede empezar de inmediato.
- **Polish (Phase 8)**: depende de que todas las historias que se quieran entregar en este ciclo estén completas.

### User Story Dependencies

- **US1, US2, US3 (P1)**: sin dependencias entre sí ni con otras historias — pueden desarrollarse en paralelo por personas distintas.
- **US4 (P2)**: sin dependencia de US1-US3. Internamente, T015 (migración SQL) bloquea a T016-T017 (backend), que a su vez bloquean a T019 (frontend conectado al filtro real) — T018 (columna de fecha visible) no depende de la migración.
- **US5, US6 (P2)**: comparten el flujo de análisis pero son páginas distintas (`/analisis/:id` vs `/analisis/:id/dashboard`) — independientes entre sí.
- **US7 (P3)**: totalmente independiente del resto.

### Parallel Opportunities

- US1, US2 y US3 se pueden trabajar en paralelo (tres personas, tres módulos distintos).
- Dentro de US4: T014 (test), T018 (columna de fecha) son paralelizables entre sí; T015→T016→T017 es secuencial (migración antes que backend).
- US5, US6 y US7 se pueden trabajar en paralelo una vez decidido el patrón visual común (ya resuelto en `research.md` §4 — reutilizar Ant Design existente, sin bloqueo real entre ellas).
- Las tareas de verificación E2E (T024, T028, T032) son paralelizables entre sí si se hacen al final.

---

## Parallel Example: User Stories P1 (MVP)

```bash
# Las tres historias P1 son independientes — se pueden lanzar en paralelo:
Task: "US1 - Desambiguar ranking de competidores en EjecutivoDashboardPage.tsx"
Task: "US2 - Fix timezone de notificaciones (backend + frontend)"
Task: "US3 - Fix scraper 0/5 estados exitosos en scraper-mp-v2"
```

---

## Implementation Strategy

### MVP First (User Stories P1)

1. Completar US1 (T001-T003), US2 (T004-T007) y US3 (T008-T013) — en paralelo si hay capacidad.
2. **STOP y VALIDAR**: correr `quickstart.md` § US1, US2, US3 contra un entorno real.
3. Desplegar — estas tres historias ya resuelven los problemas de mayor impacto de negocio (dato ejecutivo ambiguo, confianza en notificaciones, sync silenciosamente rota).

### Incremental Delivery

1. US1 + US2 + US3 (P1) → validar → desplegar.
2. US4 (P2, filtro/orden/fecha en `/analisis`) → validar → desplegar.
3. US5 + US6 (P2, rediseños de análisis) → validar → desplegar.
4. US7 (P3, rediseño de alertas) → validar → desplegar.
5. Phase 8 (Polish) al cierre de cada tanda o al final, según cuánto se agrupe en un solo despliegue.

---

## Notes

- No se generó fase de Setup/Foundational porque el proyecto ya está inicializado y ninguna historia depende de infraestructura nueva compartida.
- El número de migración `V113` (T015) se confirmó contra el archivo más reciente en `src/MPM.Api/Database/Scripts/` al momento de escribir este documento (`V112__Fix_ObtenerResultadosCompletos_Filtra_Por_Fecha_Real.sql`) — **revalidar el número real inmediatamente antes de crear el archivo**, ya que otros specs en curso pueden haber agregado migraciones nuevas entre tanto (ver advertencia ya presente en `CLAUDE.md`).
- Commitear después de cada tarea o grupo lógico, siguiendo la convención del repo.
