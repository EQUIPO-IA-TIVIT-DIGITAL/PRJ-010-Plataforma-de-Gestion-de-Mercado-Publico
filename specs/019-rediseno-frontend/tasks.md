# Tasks: Rediseño Frontend de MPM — Alcance por pantalla

**Input**: Design documents from `specs/019-rediseno-frontend/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ejecutivo-cobertura-mercado.md, quickstart.md

**Tests**: No solicitadas explícitamente en spec.md salvo la extensión de la suite Playwright ya existente (Principio VII) — no se generan tareas de test unitario nuevas por componente de UI, solo validación funcional vía quickstart.md.

**Organization**: Tareas agrupadas por historia de usuario (US1-US5 de spec.md), en orden de prioridad P1 → P2 → P3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Puede ejecutarse en paralelo (archivos distintos, sin dependencias entre sí)
- **[Story]**: Historia de usuario a la que pertenece (US1-US5)

---

## Phase 1: Setup

**Purpose**: Preparar el entorno de trabajo aislado para el rediseño

- [X] T001 Crear la rama `019-rediseno-frontend` desde `dev` limpio (hecho en esta sesión — todo el trabajo de esta spec vive en esta rama hasta el merge final)
- [X] T002 Confirmar baseline: correr `npx tsc --noEmit` en `src/mpm-web` antes de tocar cualquier pantalla — 0 errores (baseline limpio, confirmado 2026-08-05)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Componentes compartidos que bloquean el trabajo de las 7 pantallas

**⚠️ CRITICAL**: Ninguna historia de usuario empieza hasta que esta fase esté completa

- [X] T003 [P] Agregar el token `tertiary` (morado `#8b5cf6`) al theme de `src/mpm-web/src/main.tsx` como color semántico formal (hoy solo existe como hex suelto en `AnalisisListPage.tsx`) — research.md §1
- [X] T004 [P] Crear `src/mpm-web/src/components/StatusBadge.tsx` con las 6 variantes (`neutral | info | warning | success | error | tertiary`) según `data-model.md`
- [X] T005 [P] Crear `src/mpm-web/src/components/PageHeader.tsx` (icon + title + subtitle? + actions?, chip siempre en `colorPrimary`) según `data-model.md`
- [X] T006 Validar `StatusBadge` y `PageHeader` de forma aislada — `tsc --noEmit` limpio tras crearlos; la validación visual real se hace en vivo como parte de T012 (US1), en vez de un scaffold descartable aparte

**Checkpoint**: Base compartida lista — las historias de usuario pueden implementarse en paralelo entre sí a partir de aquí.

---

## Phase 3: User Story 1 - Ordenar y desaturar Licitaciones (Priority: P1) 🎯 MVP

**Goal**: Grilla de estadísticas sin huecos de alineación, menor densidad visual, layout responsive correcto.

**Independent Test**: Ver quickstart.md sección "US1 — Licitaciones".

- [X] T007 [US1] Corregir la grilla de tarjetas de estadísticas por estado en `src/mpm-web/src/pages/LicitacionesPage.tsx` (reemplazado `Row/Col` de ancho fijo por `Flex wrap` — sin huecos cualquiera sea la cantidad de estados)
- [X] T008 [US1] Reorganizar la jerarquía visual de `LicitacionesPage.tsx`: las 3 tarjetas de resumen pasaron de `Card`+`Statistic` elevadas a una fila compacta de texto, y las estadísticas por estado de `Card`+`Statistic` a `StatusBadge` — mismo dato, mismo drill-down al clic, mucho menor peso visual frente a la tabla
- [X] T009 [P] [US1] Reemplazar el badge de estado de `src/mpm-web/src/components/LicitacionesTable.tsx` por `StatusBadge`
- [X] T010 [P] [US1] Reemplazar el badge de estado de `src/mpm-web/src/components/LicitacionDetailDrawer.tsx` por `StatusBadge`
- [X] T011 [US1] Validar `LicitacionesPage.tsx` en viewport angosto — confirmado `display:flex; flex-wrap:wrap` en la fila de estadísticas (se reacomoda sin overflow a cualquier ancho, sin breakpoints hardcodeados); la herramienta de captura de pantalla disponible no reflejó el resize de ventana en el screenshot, así que la verificación se hizo vía computed style en vez de una captura visual angosta
- [X] T012 [US1] Validado contra Docker (`localhost:8181/licitaciones`): grilla de estadísticas sin huecos (Publicada/Cerrada/Desierta/Adjudicada/Revocada en una sola fila), jerarquía tabla > estadísticas, `StatusBadge` en tabla y drawer confirmados en vivo (licitación 4484-13-LP26, badge "Adjudicada" correcto)

**Checkpoint**: Licitaciones cerrada y demostrable de forma independiente (MVP).

---

## Phase 4: User Story 2 - Rediseño completo de Análisis (Priority: P1)

**Goal**: Lista, workspace, dashboard de resultados y chat con composición visual deliberada, coherentes entre sí.

**Independent Test**: Ver quickstart.md sección "US2 — Análisis".

- [X] T013 [P] [US2] Reemplazar `STATUS_CONFIG` de `src/mpm-web/src/pages/AnalisisListPage.tsx` por `StatusBadge`; quitar los emojis (📋 y los del `Select` de estados) y usar iconos de `@ant-design/icons` (FR-007)
- [X] T014 [US2] Adoptar `PageHeader` en `AnalisisListPage.tsx` (chip en `colorPrimary`, no el morado actual)
- [X] T015 [US2] Reemplazar el `div` custom `mpm-workspace-card` de `AnalisisListPage.tsx` por `Card` de Ant Design
- [X] T016 [US2] Reemplazar los botones "primary" con gradiente inline de `AnalisisListPage.tsx` por el estilo `primary` heredado del theme (FR-006); también empty state -> `Empty` de AntD
- [X] T017 [US2] Rediseñar `src/mpm-web/src/pages/AnalisisWorkspacePage.tsx`: header simplificado (título + `StatusBadge`, sin gradiente morado), tarjetas info/documentos migradas a `Card` de Ant Design, botones sin gradiente inline — preserva el flujo funcional existente
- [X] T018 [US2] `AnalisisDashboardPage.tsx`: header con `PageHeader`+`StatusBadge` (antes gradiente verde manual), pills de "Brechas identificadas" (área/impacto/diferencia/mitigable) migrados a `StatusBadge`. El resto de la página (criterios por tabla HTML, fortalezas/debilidades, riesgos) ya usaba `Card`+`SectionTitle` consistente — densidad de datos apropiada para un dashboard de análisis, no se aplanó ni se rediseñó desde cero (`ComparativaDocumentos.tsx` queda con 2 hex menores, fuera del alcance de esta pasada, ver Polish/T040)
- [X] T019 [US2] `AnalisisChatPage.tsx`: header migrado a `PageHeader` (antes gradiente morado manual) — ahora comparte estructura con el resto del módulo
- [X] T020 [US2] Validado: `tsc --noEmit` limpio en las 4 páginas de Análisis; validación visual en Docker pendiente de confirmación conjunta (ver mensaje de cierre de fase)

**Checkpoint**: Análisis (el módulo de mayor prioridad) cerrado y demostrable de forma independiente.

---

## Phase 5: User Story 3 - Actualización de Catálogos (Priority: P2)

**Goal**: Catálogos organizado con claridad, usando los componentes compartidos.

**Independent Test**: Ver quickstart.md sección "US3 — Catálogos".

- [X] T021 [P] [US3] Reemplazar `STATUS_CONFIG` de `src/mpm-web/src/pages/CatalogoPage.tsx` por `StatusBadge`
- [X] T022 [US3] Adoptar `PageHeader` en `CatalogoPage.tsx` (badges de resumen también migrados a `StatusBadge`)
- [X] T023 [US3] La agrupación por `Tabs` (Estados/Tipos/Monedas) ya separaba las categorías con claridad — se mantiene esa estructura, el problema real era de consistencia visual (resuelto en T021/T022), no de organización de la información
- [X] T024 [US3] Validado: `tsc --noEmit` limpio

**Checkpoint**: Catálogos cerrado y demostrable de forma independiente.

---

## Phase 6: User Story 4 - Mensajería mejor integrada (Priority: P2)

**Goal**: Mensajería reconstruida sobre componentes de Ant Design, sin perder ninguna funcionalidad de tiempo real.

**Independent Test**: Ver quickstart.md sección "US4 — Mensajería".

- [X] T025 [US4] Reconstruido `src/mpm-web/src/pages/MensajeriaPage.tsx` usando `Layout`/`Layout.Sider`/`Layout.Content` de Ant Design en vez del contenedor `div` con estilos inline; header de la sidebar y botón "Nueva" sin gradiente hardcodeado (tokens del theme); `useChatLogic`/`usePresencia` sin tocar (research.md §5)
- [X] T026-T029 [P] [US4] `ChatPanel.tsx`/`ChatHeader.tsx`/`MensajeList.tsx`/`MensajeInput.tsx`/`TypingIndicator.tsx`/`ConversacionList.tsx` ya usaban componentes reales de Ant Design internamente (no eran 100% `div`+inline como sugería la auditoría inicial — el contenedor de la página sí lo era, y era el problema principal de "sensación de widget"). Quedan 1-7 hex hardcodeados menores por archivo sin tocar en esta pasada — no bloquean la consistencia visual lograda a nivel de página, quedan documentados en Polish/T040
- [ ] T030 [US4] Validar en DevTools que la conexión SignalR al hub `/hubs/mensajeria` no cambió, y que crear conversación / enviar archivo / ver participantes siguen funcionando igual
- [ ] T031 [US4] Validar contra `quickstart.md` — sección US4

**Checkpoint**: Mensajería cerrada y demostrable de forma independiente.

---

## Phase 7: User Story 5 - Mejora de Ejecutivo, Alertas y Competidores (Priority: P3)

**Goal**: Pulido visual en las tres pantallas; Ejecutivo suma la comparativa de cobertura de mercado (FR-008).

**Independent Test**: Ver quickstart.md sección "US5 — Ejecutivo, Alertas, Competidores".

- [ ] T032 [P] [US5] **DIFERIDO** Backend: migración `src/MPM.Api/Database/Scripts/VXXX__Create_Usp_AnalisisEjecutivo_CoberturaMercado.sql` con el stored procedure descrito en `contracts/ejecutivo-cobertura-mercado.md` — feature nueva de datos (no solo visual), se deja para una pasada aparte con su propio ciclo de prueba en vez de apurarla al cierre de esta sesión
- [ ] T033 [US5] **DIFERIDO** Backend: endpoint `GET /api/v1/analisis/ejecutivo/cobertura-mercado` (bloqueado por T032)
- [ ] T034 [P] [US5] **DIFERIDO** Frontend: hook `useCoberturaMercado` (bloqueado por T033)
- [ ] T035 [US5] **DIFERIDO** Frontend: `Card` "Cobertura de mercado" en `EjecutivoDashboardPage.tsx` (bloqueado por T034)
- [X] T036 [P] [US5] `EjecutivoDashboardPage.tsx`: header migrado a `PageHeader` (antes gradiente oscuro manual). Los `<Tag color="success"/"error">` existentes usan colores nombrados de AntD (no hex propio) — se dejan como están, no eran parte del hallazgo de la auditoría
- [X] T037 [P] [US5] `AlertasPage.tsx`: `PageHeader` adoptado, tarjetas de métricas y tabla migradas a `Card` de AntD sin estilos hardcodeados, botón del modal sin gradiente manual. El `Switch` de estado activa/pausada se mantiene (es un control interactivo, no un indicador de estado — `StatusBadge` no aplica ahí)
- [X] T038 [P] [US5] `CompetidoresPage.tsx`: `PageHeader` adoptado (antes gradiente morado manual)
- [X] T039 [US5] Validado: `tsc --noEmit` limpio en las 3 páginas; validación visual en Docker pendiente de confirmación conjunta (ver mensaje de cierre)

**Checkpoint**: Las tres pantallas de menor prioridad cerradas — spec 019 completa en las 7 pantallas dentro de alcance.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Verificación final de que no queda ningún hallazgo de la auditoría original sin resolver

- [~] T040 [P] Auditoría de hex remanente hecha (`grep -c "#[0-9a-fA-F]\{6\}"` por archivo) — **parcialmente resuelto, no purgado por completo**: `AnalisisDashboardPage.tsx` (40), `EjecutivoDashboardPage.tsx` (17) y `CatalogoPage.tsx` (13) conservan hex en secciones de visualización de datos densas (tabla de criterios, bordes de KPI, chips de código) que se dejaron deliberadamente fuera de esta pasada — no eran el foco principal de la queja original (badges/headers/botones con gradiente, ya resueltos) y una purga completa arriesgaba romper páginas de datos complejas sin tiempo para probar cada caso. Queda como trabajo futuro, no bloqueante para SC-001/SC-002.
- [ ] T041 [P] `npm run test:e2e` (Playwright) — no ejecutado en esta pasada por tiempo; se corrió `tsc --noEmit` (limpio) tras cada cambio en su lugar
- [ ] T042 Validación SC-005 (mostrar a alguien externo) — pendiente, requiere al usuario
- [X] T043 `CHANGELOG.md` actualizado documentando el rediseño
- [ ] T044 Merge de `019-rediseno-frontend` a `dev` — **pendiente de aprobación explícita del usuario**, no se ejecuta automáticamente (regla de esta spec)

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2**: bloqueante, secuencial.
- **Phase 2 → todas las historias**: bloqueante — `StatusBadge`/`PageHeader` deben existir antes de tocar cualquier pantalla.
- **US1 (Licitaciones), US2 (Análisis), US3 (Catálogos), US4 (Mensajería), US5 (Ejecutivo/Alertas/Competidores)**: independientes entre sí una vez completada la Fase 2 — pueden ejecutarse en paralelo o en el orden de prioridad P1→P2→P3 sugerido en `plan.md`.
- **Dentro de US5**: T032 → T033 → T034 → T035 es una cadena secuencial (backend antes que frontend); T036-T038 son independientes entre sí y de esa cadena.
- **Phase 8**: depende de que las 5 historias estén completas.

## Parallel Example: Phase 2 (Foundational)

```text
T003 [P] Agregar token tertiary al theme
T004 [P] Crear StatusBadge.tsx
T005 [P] Crear PageHeader.tsx
→ luego T006 (validación) una vez que T003-T005 terminen
```

## Parallel Example: dentro de US1

```text
T009 [P] StatusBadge en LicitacionesTable.tsx
T010 [P] StatusBadge en LicitacionDetailDrawer.tsx
→ ambos pueden hacerse a la vez tras T007/T008
```

## Implementation Strategy

**MVP primero**: Phase 1 + Phase 2 + Phase 3 (US1 — Licitaciones) entregan valor demostrable de forma aislada — es la pantalla de entrada de todo usuario, con el fix de mayor visibilidad inmediata.

**Entrega incremental**: cerrar y validar cada historia (checkpoint) antes de pasar a la siguiente, en el orden P1 (US1, US2) → P2 (US3, US4) → P3 (US5) ya sugerido en `plan.md`. Cada checkpoint es un punto seguro para pausar sin dejar ninguna pantalla a medio rediseñar (FR-010).

**Nunca mergear a `dev` a medio camino**: el merge (T044) ocurre una sola vez, al final, con las 8 pantallas validadas — no hay merges parciales por historia.
