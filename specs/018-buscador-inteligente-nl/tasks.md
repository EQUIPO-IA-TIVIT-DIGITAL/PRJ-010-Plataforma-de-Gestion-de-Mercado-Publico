---

description: "Task list for 018-buscador-inteligente-nl"
---

# Tasks: Buscador Inteligente en Lenguaje Natural sobre Licitaciones

**Input**: Design documents from `specs/018-buscador-inteligente-nl/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/buscar-natural-api.md, quickstart.md

**Tests**: Se incluyen tareas de test unitario para la lógica de interpretación/mapeo (mockeable), siguiendo la convención de xUnit+Moq+FluentAssertions del resto de módulos (ver CLAUDE.md). No se agregan contract/integration tests de red real contra Vertex AI — mismo criterio que `SinonimosIaService`, que tampoco los tiene, porque dependen de credenciales ADC reales y de red externa.

**Organization**: Tareas agrupadas por user story del spec — US1 y US2 son P1 (MVP conjunto), US3 es P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Se puede ejecutar en paralelo (archivos distintos, sin dependencias)
- **[Story]**: US1, US2, US3 según spec.md
- Todas las rutas son relativas a la raíz del repo

---

## Phase 1: Setup

**Purpose**: Confirmar el estado real del repo antes de escribir código — no hay inicialización de proyecto nueva porque el módulo ya existe.

- [X] T001 Confirmar la última migración aplicada listando `src/MPM.Api/Database/Scripts/V*.sql` — usar como base real en vez de asumir V107 (research.md la estimaba a fecha de planning, puede haber avanzado)
- [X] T002 Confirmar que `GOOGLE_CLOUD_PROJECT` y `Vertex:Region` están configurados en el entorno de desarrollo local (mismo requisito que `SinonimosIaService`) — sin esto solo se puede validar el camino de fallback (Escenario 4 de quickstart.md)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Construir `ConsultaSemanticaService` — lo necesitan tanto US1 como US2 (US2 lo usa solo para el mapeo estado→código si el usuario no pasa `estado` explícito).

**⚠️ CRITICAL**: Ninguna user story puede completarse sin esta fase.

- [X] T003 Crear `ConsultaSemanticaResult` (interpretación no persistida: `TerminosExpandidos`, `EstadoInferido`, `MontoDesde`/`MontoHasta`, `FechaDesde`/`FechaHasta`, `Confianza`) en `src/MPM.Modules.Licitaciones/Models/ConsultaSemanticaResult.cs` — ver data-model.md
- [X] T004 Crear `ConsultaSemanticaService.InterpretarAsync(string q)` en `src/MPM.Modules.Licitaciones/Services/ConsultaSemanticaService.cs`, replicando el patrón de `src/MPM.Modules.Alertas/Services/SinonimosIaService.cs`: `HttpClient` + `GoogleAdcTokenProvider` (de `MPM.Shared.Services`, ya usado en el módulo), modelo `gemini-2.5-flash-lite`, `responseMimeType: "application/json"`, prompt que pide en una sola llamada sinónimos + estado/monto/fecha inferidos + `confianza`, y `try/catch` con fallback a `null` si Vertex falla o `GOOGLE_CLOUD_PROJECT` no está configurado (no debe romper la búsqueda — FR-005)
- [X] T005 [P] Registrar `ConsultaSemanticaService` y su `HttpClient` en `src/MPM.Modules.Licitaciones/ModuleRegistration.cs` (mismo patrón de registro que Alertas para `SinonimosIaService`)
- [X] T006 [P] Unit test de `ConsultaSemanticaService`: parseo de la respuesta JSON de Gemini (casos: JSON válido completo, JSON con fences ```json```, respuesta vacía, `confianza: baja`) en `tests/MPM.Modules.Licitaciones.Tests/Services/ConsultaSemanticaServiceTests.cs`, mockeando `HttpClient`/`GoogleAdcTokenProvider` — no llama a Vertex real

**Checkpoint**: `ConsultaSemanticaService` interpretando queries de prueba localmente (con mock), listo para conectarse a las user stories.

---

## Phase 3: User Story 1 - Consulta en lenguaje natural con sinónimos y filtros implícitos (Priority: P1) 🎯 MVP

**Goal**: Una consulta como "ciberseguridad para el sector salud" devuelve licitaciones que usan sinónimos ("SOC", "seguridad de la información") sin requerir el término literal, con filtros de monto/fecha detectados automáticamente si la consulta los menciona.

**Independent Test**: Ejecutar `GET /api/v1/licitaciones/buscar-natural?q=ciberseguridad%20para%20el%20sector%20salud` y confirmar que `items` incluye resultados sin la palabra literal en `nombre`/`descripcion` (Escenario 1 de quickstart.md).

### Implementation for User Story 1

- [X] T007 [US1] Extender `LicitacionService.BuscarNaturalAsync` en `src/MPM.Modules.Licitaciones/Services/LicitacionService.cs` para llamar primero a `ConsultaSemanticaService.InterpretarAsync(q)`; si `Confianza = Alta`, enriquecer el texto de búsqueda con `TerminosExpandidos` y completar `estado`/rangos de fecha inferidos SOLO si el usuario no los pasó explícitos (el parámetro explícito de `estado` siempre gana — ver contracts/buscar-natural-api.md); si la interpretación es `null` o `Confianza = Baja`, usar `q` tal cual (comportamiento actual sin cambios)
- [X] T008 [US1] Mapear `MontoDesde`/`MontoHasta` inferidos a un filtro real sobre `monto_estimado`: extender `usp_Licitaciones_Listar` (o crear el parámetro si no existe) en una migración nueva `src/MPM.Api/Database/Scripts/V<siguiente>__Add_Monto_Filter_Listar.sql` — confirmar el número exacto contra T001 antes de nombrarla
- [X] T009 [US1] Actualizar `LicitacionStoredProcedures.cs` (`src/MPM.Modules.Licitaciones/Data/LicitacionStoredProcedures.cs`) y `LicitacionHandler.BuscarNaturalAsync` (`src/MPM.Modules.Licitaciones/Data/LicitacionHandler.cs:195-224`) para pasar el texto enriquecido y los nuevos parámetros de monto a la SP
- [X] T010 [US1] Agregar manejo del edge case "consulta sin palabras reconocibles del dominio" (spec.md, Edge Cases): si `Confianza = Baja` y la búsqueda literal tampoco encuentra resultados, devolver `items: []` con `200` — nunca error (ya es el comportamiento base de `plainto_tsquery`, solo confirmar que la rama de interpretación no lo rompe)
- [X] T011 [P] [US1] Unit test de `LicitacionService.BuscarNaturalAsync`: casos con interpretación exitosa (verifica que el texto enriquecido llega al handler), interpretación `null` (fallback), y `estado` explícito con prioridad sobre el inferido, en `tests/MPM.Modules.Licitaciones.Tests/Services/LicitacionServiceTests.cs`
- [X] T012 [US1] Reactivar `useBuscarNatural` en `src/mpm-web/src/hooks/useLicitaciones.ts:40-55` como el hook detrás de la nueva barra de búsqueda (confirmar que sigue con `enabled: q.length >= 2`, sin cambios de contrato necesarios)
- [X] T013 [US1] Crear componente de barra de búsqueda semántica (o extender `src/mpm-web/src/components/LicitacionFilterBar.tsx`) para conectar a `useBuscarNatural` en vez de al filtro `search` actual de `useLicitaciones`
- [X] T014 [US1] Conectar la barra nueva en `src/mpm-web/src/pages/LicitacionesPage.tsx`, decidiendo si reemplaza el input de texto libre actual de `LicitacionFilterBar` o convive con él (evaluar durante implementación, documentar la decisión en el propio PR)

**Checkpoint**: US1 funcional de punta a punta — consulta en lenguaje natural en la UI devuelve resultados con sinónimos y filtros implícitos aplicados.

---

## Phase 4: User Story 2 - Filtrado por estado de licitación (Priority: P1)

**Goal**: El usuario puede acotar la búsqueda semántica a activas/cerradas/adjudicadas sin perder el ranking semántico, y ese filtro explícito de UI siempre gana sobre lo que la IA infiera del texto.

**Independent Test**: Buscar "telecomunicaciones" con el selector de estado en "Adjudicadas" y confirmar que ningún resultado activo aparece (Escenario 3 de quickstart.md).

### Implementation for User Story 2

- [X] T015 [US2] Confirmar en `LicitacionController.cs:137-152` que el parámetro `estado` del query string sigue llegando intacto a `LicitacionService.BuscarNaturalAsync` y que T007 respeta su prioridad sobre `EstadoInferido` (esto ya debería quedar cubierto por T007 — esta tarea es la verificación explícita + el test correspondiente)
- [X] T016 [P] [US2] Unit test: dado `estado` explícito en la request Y un `EstadoInferido` distinto en la interpretación IA, el resultado usa el `estado` explícito — agregar caso en `tests/MPM.Modules.Licitaciones.Tests/Services/LicitacionServiceTests.cs` (mismo archivo de T011)
- [X] T017 [US2] Confirmar en el frontend (`src/mpm-web/src/components/LicitacionFilterBar.tsx` o el componente nuevo de T013) que el selector de Estado ya existente sigue visible y funcional junto a la barra de búsqueda semántica, enviando `estado` como parámetro explícito

**Checkpoint**: US1 + US2 funcionando juntas — búsqueda semántica respetando siempre el filtro de estado explícito del usuario.

---

## Phase 5: User Story 3 - Resumen antes de descargar documentos (Priority: P2)

**Goal**: Los resultados muestran objeto, organismo, monto y fecha de cierre sin disparar ninguna descarga de PDF; solo al abrir el detalle se accede a los documentos completos.

**Independent Test**: Ejecutar una búsqueda y confirmar con `read_network_requests` que no hay descargas de adjuntos disparadas solo por renderizar la lista de resultados (Escenario 5 de quickstart.md).

### Implementation for User Story 3

- [X] T018 [US3] Confirmar que las tarjetas de resultado en el componente de US1 (T013/T014) usan únicamente los campos de `LicitacionNaturalSearchResult` (`Id, CodigoExterno, Nombre, Descripcion, Organismo, CodigoEstado, Tipo, Relevancia`) sin llamar a ningún endpoint de detalle/adjuntos al renderizar
- [X] T019 [US3] Confirmar que el clic en un resultado navega a la ficha existente de la licitación (ruta ya existente en el módulo de Licitaciones) en vez de disparar una descarga inline

**Checkpoint**: Las tres user stories funcionan de forma independiente y en conjunto.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T020 Ejecutar los escenarios de `specs/018-buscador-inteligente-nl/quickstart.md` de punta a punta contra Docker real (`docker compose up -d --build api`), con ADC/Vertex activo. Escenarios 1 (sinónimos), 3 (prioridad de estado explícito) y el camino de degradación (FR-005, disparado orgánicamente por un 429 real de Vertex, no simulado) verificados en vivo el 2026-07-16. Encontró y corrigió un bug real de integración (ver Hallazgos abajo). Escenario 5 (cero descargas) validado por inspección de código, no por captura de red en navegador — pendiente si se quiere confirmar visualmente.
- [X] T021 Medido SC-001 en vivo — **NO se cumple localmente** (p95 muy por encima de 3s, ver cifras abajo). **Decisión del usuario (2026-07-16)**: se atribuye a latencia de red del entorno local de desarrollo contra `us-central1`/cuota del proyecto de desarrollo, no representativa de producción — no bloquea el cierre del spec. Revisar la latencia real una vez desplegado, sin acción de seguimiento inmediata.
- [X] T022 Recall de sinónimos observado cualitativamente en vivo: "ciberseguridad para el sector salud" encontró el Hospital San Carlos por "ciberseguridad" y otros resultados vía "seguridad informática" sin el término literal completo — recall funciona, pero la expansión parece generar bastantes falsos positivos vía la palabra genérica "seguridad" (ver Hallazgos). No se corrió el benchmark formal de 20 queries con scoring contra el set de palabras clave del equipo — requeriría más cuota de la disponible en esta sesión.

### Hallazgos de la validación en vivo (2026-07-16, Docker + Vertex AI real)

1. **Bug real encontrado y corregido**: `usp_Licitaciones_BuscarNatural` fallaba con `42883` (`function ... does not exist`) porque `LicitacionHandler.BuscarNaturalAsync` pasaba `p_fecha_desde` (string sin tipo) y `p_fecha_hasta` (`DateTime?` en `null`) vía objeto anónimo de Dapper — mismo bug de fondo que BUG-014 (parámetros sin `DbType` explícito viajan como `unknown`/tipo incorrecto y Postgres no resuelve el overload). Corregido con `DynamicParameters` + `DbType.Date` explícito, igual que el patrón ya usado en `ActualizarDetalleAsync`. Este bug es anterior a esta feature (el endpoint nunca había sido llamado con tráfico real, según la investigación de research.md) pero solo se manifestó al probarlo en vivo.
2. **SC-001 (latencia <3s) no se cumple**: la llamada a Gemini (no la SQL) domina el tiempo. Muestras reales de `gemini-2.5-flash-lite` contra `tivit-cu010`/`us-central1`: 828ms, 964ms, 1622ms, 5060ms, 6094ms, 7609ms, 8676ms, 10896ms (este último terminó en `429 Resource exhausted`), 11440ms. Mediana ~6s, claramente sobre el objetivo. **No parece ser un problema del modelo en sí** (hay respuestas de 828ms-1.6s) sino de **cuota/capacidad del proyecto GCP de desarrollo** — el 429 real confirma que se está chocando contra un límite de cuota, lo que también explica la cola/latencia alta en las demás llamadas. Recomendación: antes de escalar a `gemini-3.1-flash-lite` (que no resolvería un problema de cuota), revisar la cuota de Vertex AI asignada a `tivit-cu010` para `gemini-2.5-flash-lite` — es probable que sea un proyecto de desarrollo con cuota baja, no representativo de producción.
3. **FR-005 (degradación) confirmado en un caso real, no solo simulado**: cuando Vertex devolvió 429, la búsqueda igual respondió `200` con resultados (fallback a búsqueda literal), sin que el usuario viera un error. Esto valida el diseño más que cualquier prueba simulada.
4. **Filtro de estado explícito (US2) confirmado**: con `estado=8` en la request, el 100% de los resultados devueltos tienen `codigoEstado=8`, sin excepción, incluso con interpretación IA activa.
5. **Posible sobre-expansión de sinónimos**: la consulta "telecomunicaciones" con `estado=8` devolvió 10,176 resultados de un universo de 120,970 licitaciones adjudicadas (~8.4%) — sugiere que el prompt de `ConsultaSemanticaService` puede estar generando sinónimos demasiado genéricos (p. ej. "seguridad" solo, que matchea casi cualquier licitación con esa palabra). No es un bug de la lógica de filtrado (el estado se respeta perfectamente), es un tema de precisión del prompt — candidato a ajustar el prompt (pedir sinónimos más específicos, o penalizar términos de una sola palabra muy genéricos) si se confirma con el benchmark formal pendiente de T022.
- [X] T023 [P] Actualizar `specs/018-buscador-inteligente-nl/spec.md` marcando el `Status` como implementado y registrando qué edge cases quedaron cubiertos vs. pendientes (en particular, el filtro de "ubicación implícita" que quedó fuera de alcance — ver research.md)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias — puede iniciar de inmediato
- **Foundational (Phase 2)**: depende de Setup — bloquea las tres user stories
- **US1 (Phase 3)** y **US2 (Phase 4)**: ambas dependen solo de Foundational; son P1 y forman el MVP conjunto (US2 es en gran parte verificación de una propiedad que T007 ya implementa, así que en la práctica se completan casi simultáneamente)
- **US3 (Phase 5)**: depende de que exista el componente de resultados de US1 (T013/T014) para poder verificar que no dispara descargas — no requiere lógica de backend propia
- **Polish (Phase 6)**: depende de las tres user stories completas

### Parallel Opportunities

- T005 y T006 en paralelo tras T004
- T011 en paralelo con T012-T014 (backend vs. frontend de US1)
- T016 puede ejecutarse en paralelo con el trabajo de frontend de US2 (T017)
- T023 en paralelo con T020-T022

---

## Implementation Strategy

### MVP First

1. Completar Phase 1 (Setup) y Phase 2 (Foundational — `ConsultaSemanticaService`)
2. Completar Phase 3 (US1) — ya es un MVP demostrable: búsqueda semántica funcionando en la UI
3. Completar Phase 4 (US2) — en la práctica, ya viene resuelto por el diseño de T007; esta fase es sobre todo verificación explícita
4. **Detener y validar** con quickstart.md Escenarios 1-3
5. Continuar con Phase 5 (US3) y Phase 6 (Polish) antes de dar la feature por cerrada

### Incremental Delivery

1. Setup + Foundational → base lista
2. US1 → demo de búsqueda semántica con sinónimos (valor visible inmediato)
3. US2 → confirma que el filtro explícito de estado no se rompe con la nueva capa de IA
4. US3 → confirma la propiedad de "cero descargas al explorar", ya casi gratis si T013/T014 se hicieron bien
5. Polish → medición real de SC-001/SC-002, que puede forzar volver a research.md si el modelo `flash-lite` no alcanza
