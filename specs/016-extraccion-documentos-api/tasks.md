# Tasks: Extracción de Documentos vía API Directa

**Input**: Design documents from `/specs/016-extraccion-documentos-api/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/internal-api.md, quickstart.md

**Tests**: La constitución (principio VII) exige unit tests para código nuevo. El acceso real al portal se valida manualmente (quickstart), no en CI.

**Organization**: Tareas agrupadas por user story. El spike (T004) es bloqueante del resto de la implementación directa.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede ejecutarse en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: user story a la que pertenece (US1–US3)

## Path Conventions

Backend `src/MPM.Modules.Licitaciones/`; migraciones `src/MPM.Api/Database/Scripts/`; scraper Node `tools/scraper-mp/` (fallback, sin cambios).

---

## Phase 1: Setup

- [ ] T001 Agregar dependencia `AngleSharp` al proyecto `src/MPM.Modules.Licitaciones/MPM.Modules.Licitaciones.csproj` y verificar `dotnet build`
- [ ] T002 Agregar claves de configuración `Extraccion:*` (Modo, SesionTtlHoras, MaxConcurrencia, DelayMs) a `appsettings.json` y a `docker-compose.yml` con sus defaults (ver data-model.md §5)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: persistencia y descubrimiento del portal — prerequisitos de todo el flujo directo

- [ ] T003 Migración `src/MPM.Api/Database/Scripts/V077__Extraccion_documentos.sql`: columna `metodo_extraccion` en `licitaciones_adjuntos`; tabla `extraccion_documentos_log` (data-model.md §2); SPs `usp_ExtraccionLog_Registrar`, `usp_ExtraccionLog_ResumenPeriodo`, `usp_Adjuntos_ExistePorLicitacion`
- [ ] T004 **SPIKE (bloqueante)** de descubrimiento del portal (research.md R1 / quickstart Fase 0): capturar tráfico real (HAR/trace) de listar+descargar Acta en 2-3 licitaciones conocidas; completar la sección 1 de `contracts/internal-api.md` con URLs, cookies, campos WebForms y request de descarga. Criterio de salida: reproducir la descarga con curl/Postman usando solo HTTP+cookies

**Checkpoint**: contrato del portal documentado y verificado manualmente — puede comenzar la implementación directa

---

## Phase 3: User Story 1 - Descarga directa sin navegador (Priority: P1) 🎯 MVP

**Goal**: obtener Acta + adjuntos por HTTP directo, sin navegador por licitación.

**Independent Test**: sobre una licitación conocida, el flujo directo descarga el mismo Acta que el navegador, con `metodo_extraccion='directo'` y sin abrir Chromium (quickstart §1).

- [ ] T005 [US1] `src/MPM.Modules.Licitaciones/Models/ExtraccionModels.cs`: DTOs `ResultadoExtraccion`, `AdjuntoDescargado`, `LicitacionRef`, `MpSession`, `AdjuntosListado`, `AdjuntoFila`, `WebFormsState` (contracts §2)
- [ ] T006 [P] [US1] `src/MPM.Modules.Licitaciones/Services/WebFormsParser.cs`: parseo con AngleSharp de `#DWNL_grdId` (filas + detección de Acta) y campos `__VIEWSTATE`/`__VIEWSTATEGENERATOR`/`__EVENTVALIDATION`
- [ ] T007 [P] [US1] Unit tests `tests/MPM.Modules.Licitaciones.Tests/Services/WebFormsParserTests.cs` con HTML de muestra (capturado en el spike): parsea filas, identifica el Acta, extrae ViewState; maneja tabla vacía
- [ ] T008 [US1] `src/MPM.Modules.Licitaciones/Services/MpSessionProvider.cs`: obtener cookies invocando el login Node (`tools/scraper-mp`), cachear en Redis con TTL `Extraccion:SesionTtlHoras`, renovar con lock ante expiración/forzado (research R2)
- [ ] T009 [P] [US1] Unit tests `tests/MPM.Modules.Licitaciones.Tests/Services/MpSessionProviderTests.cs`: usa cookie cacheada dentro del TTL; renueva al expirar; una sola renovación concurrente (lock)
- [ ] T010 [US1] `src/MPM.Modules.Licitaciones/Services/AdjuntosHttpExtractor.cs`: GET listado con cookies → `WebFormsParser` → POST postback por documento objetivo (Acta + bases/anexos/resoluciones, FR-007) → guardar vía `IStorageService` → persistir en `licitaciones_adjuntos` con `metodo_extraccion='directo'`; señalar 401/403 para renovación
- [ ] T011 [P] [US1] Unit tests `tests/MPM.Modules.Licitaciones.Tests/Services/AdjuntosHttpExtractorTests.cs` con `HttpClient` mockeado (HttpMessageHandler): arma el postback correcto, guarda el stream, mapea 401/403 a señal de renovación

**Checkpoint**: US1 demostrable — descarga directa funcional sobre licitaciones conocidas

---

## Phase 4: User Story 2 - Continuidad ante fallas (Priority: P2)

**Goal**: fallback automático al navegador y registro de fallos reales.

**Independent Test**: forzar fallo del directo → cae al navegador; forzar fallo de ambos → un fallo real consultable (quickstart §2).

- [ ] T012 [US2] `src/MPM.Modules.Licitaciones/Data/ExtraccionLogHandler.cs`: llamadas Dapper a `usp_ExtraccionLog_Registrar` / `usp_ExtraccionLog_ResumenPeriodo` / `usp_Adjuntos_ExistePorLicitacion`
- [ ] T013 [US2] `src/MPM.Modules.Licitaciones/Services/DocumentExtractionService.cs`: orquesta directo → fallback Node → registro en `extraccion_documentos_log`; fallo real solo si ambos fallan (FR-006); idempotencia vía `usp_Adjuntos_ExistePorLicitacion` (research R4/R7)
- [ ] T014 [P] [US2] Unit tests `tests/MPM.Modules.Licitaciones.Tests/Services/DocumentExtractionServiceTests.cs`: éxito directo (no invoca fallback); fallo directo → invoca fallback; ambos fallan → registro de fallo real; licitación ya procesada → no reprocesa

**Checkpoint**: US2 completa — sin huecos silenciosos

---

## Phase 5: User Story 3 - Validación en paralelo y adopción (Priority: P3)

**Goal**: modo configurable y comparación de cobertura antes de retirar el navegador.

**Independent Test**: en modo `paralelo`, `usp_ExtraccionLog_ResumenPeriodo` permite comparar directo vs. navegador por licitación (quickstart §3).

- [ ] T015 [US3] Implementar el flag `Extraccion:Modo` (`solo_navegador` | `paralelo` | `directo_con_fallback`) en `DocumentExtractionService` (research R5); default `solo_navegador`
- [ ] T016 [US3] Integrar `DocumentExtractionService` en `src/MPM.Modules.Licitaciones/Services/SyncEngineService.cs` (invocarlo en el ciclo en lugar de disparar el scraper directamente), respetando concurrencia `Extraccion:MaxConcurrencia` y `Extraccion:DelayMs` (research R6)
- [ ] T017 [P] [US3] Registrar los servicios nuevos en `ModuleRegistration.cs` de `MPM.Modules.Licitaciones` (DI: `MpSessionProvider`, `WebFormsParser`, `AdjuntosHttpExtractor`, `DocumentExtractionService`, `ExtraccionLogHandler`, `HttpClient` con `SocketsHttpHandler`/`CookieContainer`)
- [ ] T018 [P] [US3] Unit test de resumen de comparación en `tests/MPM.Modules.Licitaciones.Tests` (modo paralelo produce ambos registros; el resumen agrega por método/estado)

**Checkpoint**: US3 completa — transición gradual y medible

---

## Phase 6: Polish & Cross-Cutting

- [ ] T019 Robustez anti-automatización (research R6): User-Agent/Referer realistas, semáforo de concurrencia, backoff ante 429, licitación sin adjuntos = `sin_adjuntos`; cubrir con tests donde aplique
- [ ] T020 Validación manual con stack levantado (quickstart §1-5) sobre las licitaciones baseline; documentar métricas directo vs. navegador (SC-001/SC-002/SC-003/SC-005)
- [ ] T021 [P] Actualizar `CHANGELOG.md` y `docs/` con el flujo de extracción directa, el flag `Extraccion:Modo` y el procedimiento de promoción a `directo_con_fallback`

---

## Dependencies

```text
T001, T002 (setup)
  └─ T003 (migración) ── T012 (log handler)
  └─ T004 (SPIKE, bloqueante) ── habilita T006/T010 (parser y extractor dependen del contrato descubierto)
US1: T005 → (T006‖T008‖T010) con sus tests (T007‖T009‖T011)
US2: T012 → T013 → T014
US3: T015 → T016 ; T017 (DI) ; T018
Polish: T019, T020, T021
```

**Orden de historias**: Setup → Foundational (incl. SPIKE) → US1 → US2 → US3 → Polish.

## Parallel Execution Examples

- Tras el spike: `WebFormsParser` (T006), `MpSessionProvider` (T008) y sus tests pueden avanzar en paralelo; `AdjuntosHttpExtractor` (T010) integra ambos.
- Los archivos de tests (T007, T009, T011, T014, T018) son `[P]` entre sí.

## Implementation Strategy

**MVP = Fase 3 (US1)** con `Extraccion:Modo=paralelo`: descarga directa funcionando y comparándose contra el navegador sin cambiar la fuente de verdad. Solo cuando el directo iguale/supere la cobertura (SC-003/SC-005) se promueve a `directo_con_fallback`. El scraper Node nunca se elimina en este feature — su retiro es una etapa posterior fuera de alcance.

## Notas

- **El spike (T004) es el mayor riesgo del feature**: si el portal exige JavaScript no reproducible por HTTP, el flujo directo no es viable y se mantiene `solo_navegador` (el fallback garantiza que no se pierde funcionalidad).
- El login Keycloak se reutiliza desde Node (research R2); reimplementarlo en C# puro queda como mejora futura si se decide retirar Node por completo.
