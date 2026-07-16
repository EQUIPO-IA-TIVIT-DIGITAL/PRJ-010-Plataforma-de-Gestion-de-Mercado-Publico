---

description: "Task list for 027-catalogo-frontend-licitaciones-generales"
---

# Tasks: Frontend de Licitaciones Alineado al Catálogo Real de Tipos/Estados

**Input**: Design documents from `specs/027-catalogo-frontend-licitaciones-generales/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/catalogos-api.md, quickstart.md

**Tests**: Se incluye un test unitario para el nuevo tipo de `TipoLicitacionItemDto.Codigo` (mapeo Dapper), siguiendo la convención xUnit+Moq+FluentAssertions del resto del proyecto (ver `CatalogoServiceTests.cs`). No se agregan tests E2E nuevos — este cambio se valida con `quickstart.md`.

**Organization**: US1 y US2 son P1 (MVP conjunto — ambas tocan el mismo par de endpoints de catálogo), US3 es P2.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

- [X] T001 Confirmar la última migración aplicada listando `src/MPM.Api/Database/Scripts/V*.sql` — usar como base real en vez de asumir V108 (research.md la estimaba a fecha de planning)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Migración de base de datos que repuebla `tipos_licitacion` y ajusta `usp_Catalogos_EstadosLicitacion()` — la necesitan tanto US1 como US2.

**⚠️ CRITICAL**: Ninguna user story puede completarse sin esta fase.

- [X] T002 Crear migración `src/MPM.Api/Database/Scripts/V108__Reconciliar_Catalogo_Tipos_Estados.sql` (confirmar número contra T001) que: (a) altera `tipos_licitacion.codigo` de `SMALLINT` a `VARCHAR(10)` — requiere `DROP` y recrear la tabla o `ALTER COLUMN ... TYPE VARCHAR(10) USING codigo::text` más el ajuste del `PRIMARY KEY`; (b) trunca y repuebla `tipos_licitacion` con los códigos del glosario de `specs/026-robustez-sincronizacion-tipos-reales/spec.md` (LE, LP, LQ, LR, CO, CA, TD, LS, L, B, R, E, I) más los 4 códigos nuevos observados sin documentar (O, H, CI, DC) con `descripcion = 'Pendiente de documentar'`; (c) reemplaza `usp_Catalogos_EstadosLicitacion()` (`CREATE OR REPLACE FUNCTION`) para que el `SELECT` filtre `WHERE codigo IN (5,6,7,8,15)` — sin tocar el contenido de la tabla `estados_licitacion` ni las filas de `licitaciones` en código 1 (ver research.md, Decisión 2)
- [X] T003 [P] Verificar manualmente tras aplicar la migración: `SELECT codigo, nombre FROM tipos_licitacion ORDER BY codigo;` devuelve los códigos reales como texto, y `SELECT * FROM usp_Catalogos_EstadosLicitacion();` devuelve exactamente 5 filas

**Checkpoint**: Catálogo de base de datos reconciliado, listo para conectarse al backend/frontend.

---

## Phase 3: User Story 1 - Filtrar licitaciones generales por su tipo real (Priority: P1) 🎯 MVP

**Goal**: El selector de Tipo del buscador ofrece los códigos reales de licitación y cada opción devuelve resultados reales al filtrar.

**Independent Test**: Abrir el buscador de licitaciones, seleccionar cada opción del filtro de Tipo, y confirmar que cada una devuelve resultados reales (Escenario 1 de quickstart.md).

### Implementation for User Story 1

- [X] T004 [US1] Cambiar `TipoLicitacionItemDto.Codigo` de `int` a `string` en `src/MPM.Modules.Catalogo/Models/CatalogoDtos.cs:9-14`
- [X] T005 [P] [US1] Actualizar el test existente en `tests/MPM.Modules.Catalogo.Tests/Services/CatalogoServiceTests.cs` (casos `GetTiposLicitacionAsync_CallsHandler`, `GetAllAsync_CallsHandler`) para usar `Codigo` como string (ej. `"LE"` en vez de `1`), confirmando que el mapeo sigue funcionando con el nuevo tipo
- [X] T006 [US1] Cambiar `TipoLicitacionItem.codigo` de `number` a `string` en `src/mpm-web/src/types/catalogo.ts:6-10`
- [X] T007 [US1] Cambiar `LicitacionFilter.tipo` de `TipoLicitacion | null` a `string | null` en `src/mpm-web/src/types/licitacion.ts:42-53` (ver data-model.md — ya no tiene sentido una unión cerrada de 4 valores)
- [X] T008 [US1] Actualizar `tipoOptions` en `src/mpm-web/src/components/LicitacionFilterBar.tsx:30-33` para usar `value: t.codigo` (el código real) en vez de `value: t.slug`
- [X] T009 [US1] Correr el Escenario 1 de `quickstart.md` en Docker local: confirmar que `GET /api/v1/catalogos/tipos-licitacion` devuelve códigos reales como string, y que filtrar por cualquiera de ellos en `/licitaciones` devuelve resultados

**Checkpoint**: US1 funcional de punta a punta — filtro de Tipo funciona sobre datos reales.

---

## Phase 4: User Story 2 - Ver el estado real sin duplicados en el selector (Priority: P1)

**Goal**: El selector de Estado muestra exactamente 5 opciones reales, sin duplicados heredados.

**Independent Test**: Abrir el selector de Estado y confirmar que cada nombre aparece una sola vez (Escenario 2 de quickstart.md).

### Implementation for User Story 2

- [X] T010 [US2] Verificar que `LicitacionFilterBar.tsx:25-28` (`estadoOptions`) no requiere ningún cambio de código — ya mapea 1:1 desde `catalogos.estadosLicitacion`, que tras T002 devuelve solo 5 filas
- [X] T011 [US2] Correr el Escenario 2 de `quickstart.md`: confirmar que `GET /api/v1/catalogos/estados-licitacion` devuelve exactamente 5 filas y que el selector en `/licitaciones` no muestra duplicados

**Checkpoint**: US1 + US2 funcionando juntas — ambos selectores del buscador reflejan datos reales.

---

## Phase 5: User Story 3 - Ver el buscador sin columnas vacías (Priority: P2)

**Goal**: La tabla de licitaciones generales no muestra columnas de Organismo, Monto ni Items; la ficha de detalle los sigue mostrando cuando existen.

**Independent Test**: Abrir el listado y confirmar que esas 3 columnas no aparecen, luego abrir el detalle de una licitación y confirmar que si están disponibles, sí se muestran ahí (Escenario 3 de quickstart.md).

### Implementation for User Story 3

- [X] T012 [US3] Quitar la columna "Organismo" (`dataIndex: 'organismo'`) de `src/mpm-web/src/components/LicitacionesTable.tsx:172-182`
- [X] T013 [US3] Quitar la columna "Monto" (`dataIndex: 'montoEstimado'`) de `src/mpm-web/src/components/LicitacionesTable.tsx:216-234`
- [X] T014 [US3] Quitar la columna "Items" (`dataIndex: 'itemsCount'`) de `src/mpm-web/src/components/LicitacionesTable.tsx:235-251`
- [X] T015 [US3] Confirmar que `src/mpm-web/src/components/LicitacionDetailDrawer.tsx` no se modifica — sigue mostrando Organismo/Monto/Items sin cambios, apoyado en `LicitacionService.ObtenerPorCodigoAsync` (backend, sin cambios en esta spec)
- [X] T016 [US3] Correr los Escenarios 3 y 4 de `quickstart.md`: confirmar que la tabla no tiene las 3 columnas, y que abrir el detalle de una licitación de participación TIVIT (con datos completos) los sigue mostrando sin pérdida

**Checkpoint**: Las tres user stories funcionan de forma independiente y en conjunto.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T017 [P] Ejecutar `npx tsc --noEmit` en `src/mpm-web` para confirmar que los cambios de tipo (T006, T007) no rompen otros consumidores de `TipoLicitacionItem`/`LicitacionFilter` en el frontend
- [X] T018 Correr la suite de tests de `MPM.Modules.Catalogo.Tests` (vía contenedor con SDK .NET 8 si el entorno local no tiene el runtime, como en 018) y confirmar que sigue en verde tras T004/T005
- [X] T019 [P] Actualizar `specs/027-catalogo-frontend-licitaciones-generales/spec.md` marcando el `Status` como implementado tras validar los 4 escenarios de quickstart.md en Docker real

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup — bloquea las tres user stories (la migración T002 es compartida por US1 y US2)
- **US1 (Phase 3)** y **US2 (Phase 4)**: dependen de Foundational; ambas son P1 y forman el MVP. US2 requiere solo verificación (T010-T011), ya que el filtro de estados no necesita cambios de código, solo el ajuste de datos de T002
- **US3 (Phase 5)**: independiente de US1/US2 a nivel de código (toca un componente distinto), pero conceptualmente se valida después porque comparte la pantalla `/licitaciones`
- **Polish (Phase 6)**: depende de las tres user stories completas

### Parallel Opportunities

- T003 en paralelo con el resto de Foundational una vez aplicada T002
- T005 (test backend) en paralelo con T006-T008 (frontend) dentro de US1
- Toda la Phase 5 (US3) se puede trabajar en paralelo con Phase 3/4 (US1/US2) por un desarrollador distinto, ya que no comparten archivos
- T017 y T019 en paralelo con T018

---

## Implementation Strategy

### MVP First

1. Completar Phase 1 (Setup) y Phase 2 (Foundational — migración V108)
2. Completar Phase 3 (US1) — ya demostrable: filtro de Tipo funcionando sobre datos reales
3. Completar Phase 4 (US2) — en la práctica, verificación rápida ya cubierta por la misma migración
4. **Detener y validar** con quickstart.md Escenarios 1-2
5. Continuar con Phase 5 (US3) y Phase 6 (Polish)

### Incremental Delivery

1. Setup + Foundational → catálogo reconciliado en base de datos
2. US1 → filtro de Tipo utilizable (valor de negocio inmediato para el equipo comercial)
3. US2 → selector de Estado limpio (casi gratis tras la migración)
4. US3 → tabla del listado sin ruido visual, sin afectar el detalle
5. Polish → confirmación de que nada más en el frontend dependía del tipo numérico anterior
