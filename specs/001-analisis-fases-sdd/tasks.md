# Tasks: Fases 2 y 3 — Pipeline de Análisis IA + Dashboard Ejecutivo

**Input**: Design documents from `specs/001-analisis-fases-sdd/`

**Scope**: Completar la automatización del scraping de "Actas de Evaluación" desde Mercado Público y el pipeline completo con Gemini AI. No incluye test tasks (no solicitados en la spec).

**User Stories cubiertas en esta fase**:
- **US1** (P1): Análisis Automático de Licitación Perdida → core de Fase 2
- **US4** (P1): Demo Ejecutiva Funcional los Jueves → criterio de éxito transversal

**User Stories diferidas a fases futuras**:
- US2 (P1): Dashboard Comparativo Ejecutivo → Fase 3
- US3 (P2): Seguimiento de Licitaciones Activas → Fase 4

---

## Phase 1: Setup

**Purpose**: Verificar y completar los archivos base que el pipeline requiere.

- [x] T001 Verificar existencia de `tools/scraper-mp/package.json` con dependencias `playwright`, `dotenv`, `pg`; crear si no existe
- [x] T002 [P] Verificar existencia de `tools/scraper-mp/modulos/login.js` y que use `MP_RUT`/`MP_PASSWORD` desde env
- [x] T003 Crear o actualizar `.env.example` en la raíz del proyecto con las variables del scraper: `MP_RUT`, `MP_PASSWORD`, `SCRAPER_ENABLED`, `MP_ANALISIS_IA`, `MP_FECHA_DESDE`, `SCRAPER_INTERVAL_HOURS`

---

## Phase 2: Foundational — Docker e Infraestructura

**Purpose**: Cambios de infraestructura que deben completarse antes de que el pipeline pueda correr en Docker.

**⚠️ CRÍTICO**: Nada del pipeline funciona en contenedor hasta completar esta fase.

- [x] T004 Actualizar `src/MPM.Api/Dockerfile` para instalar Node.js 20 vía apt/nodesource en el stage de runtime
- [x] T005 Actualizar `src/MPM.Api/Dockerfile` para copiar `tools/scraper-mp/` al contenedor en `/app/tools/`
- [x] T006 Agregar paso `npm ci --omit=dev` en el Dockerfile para instalar dependencias del scraper dentro del contenedor
- [x] T007 Agregar paso `npx playwright install chromium --with-deps` en el Dockerfile para instalar el browser headless
- [x] T008 Actualizar `docker-compose.yml` servicio `api`: añadir variables `MP_RUT`, `MP_PASSWORD`, `SCRAPER_ENABLED`, `Scraper__Enabled`, `Scraper__IntervalHours`, `Scraper__ScriptPath=/app/tools/agente-mp.js`, `MP_ANALISIS_IA`, `MP_HEADLESS=true`, `MP_FECHA_DESDE`, `API_BASE_URL=http://localhost:80`

**Checkpoint**: `docker compose build api` debe completar sin errores y `node --version` debe funcionar dentro del contenedor.

---

## Phase 3: User Story 1 — Análisis Automático de Licitación Perdida (P1) 🎯 MVP

**Goal**: El sistema extrae automáticamente el "Acta de Evaluación" de licitaciones adjudicadas y dispara el análisis Gemini sin intervención manual.

**Independent Test**: Dado un código de licitación adjudicada, ejecutar el scraper una vez y verificar que en `licitaciones_adjuntos` aparece un registro con `tipo='acta_evaluacion'` y `analisis_estado='completado'`, y en `analisis_workspaces` hay un workspace con `estado='completado'`. Ver [quickstart.md](./quickstart.md) Escenario 2.

### Implementación US1

- [x] T009 [US1] Modificar `src/MPM.Modules.Licitaciones/Services/ScraperBackgroundService.cs`: reemplazar el path hardcodeado `"tools/scraper-mp/agente-mp.js"` por lectura desde `config["Scraper:ScriptPath"]` o `config["SCRAPER_SCRIPT_PATH"]`, con fallback a `Path.Combine(AppContext.BaseDirectory, "tools", "agente-mp.js")`
- [x] T010 [US1] Modificar `src/MPM.Modules.Licitaciones/Services/ScraperBackgroundService.cs`: en el método `EjecutarScraperAsync`, agregar al `ProcessStartInfo.EnvironmentVariables` las variables: `MP_RUT`, `MP_PASSWORD`, `MP_ANALISIS_IA`, `MP_HEADLESS`, `MP_FECHA_DESDE`, `API_BASE_URL`, `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`, `DB_HOST=db`, `DB_PORT=5432`, `DB_NAME`, `DB_USER`, `DB_PASSWORD` — leyendo cada una desde `IConfiguration`
- [x] T011 [US1] Verificar en `tools/scraper-mp/modulos/buscar.js` que `configurarFiltros()` incluye la llamada a `seleccionarFiltroOfertado()` (radio `#radLicitacionOfertado`) y que el estado seleccionado es `'8'` (Adjudicada); ajustar si se detecta discrepancia
- [x] T012 [US1] Verificar en `tools/scraper-mp/modulos/adjuntos.js` que la detección del acta usa `tipo === 'Acta de Evaluación'` (con tilde); agregar fallback por nombre si el tipo no está disponible en alguna licitación (buscar `nombre.toLowerCase().includes('acta') && nombre.toLowerCase().includes('evaluaci')`)
- [x] T013 [US1] Verificar en `tools/scraper-mp/modulos/api-client.js` que `generarServiceToken()` produce un JWT válido con el mismo `JWT_SECRET` que usa la API — confirmar que las claims `iss` y `aud` coinciden con `JWT_ISSUER` y `JWT_AUDIENCE`

**Checkpoint**: `docker compose up --build -d` + activar `SCRAPER_ENABLED=true` + verificar logs `docker compose logs api | grep -i scraper` muestra `ScraperBackgroundService starting. Interval: 12h`.

---

## Phase 4: User Story 4 — Demo Ejecutiva Funcional (P1)

**Goal**: El flujo completo es demostrable en el ambiente local: licitaciones → workspace con análisis → dashboard → chat contextual. Sin errores visibles para ejecutivos.

**Independent Test**: Ejecutar el quickstart [Escenario 4](./quickstart.md) completo en menos de 5 minutos. El dashboard muestra análisis y el chat responde preguntas.

### Implementación US4

- [x] T014 [US4] Ejecutar el scraper manualmente una vez siguiendo el [quickstart.md Escenario 1](./quickstart.md) para verificar el login y la búsqueda en Mercado Público (sin `MP_ANALISIS_IA` activado)
- [x] T015 [US4] Ejecutar el scraper con `MP_ANALISIS_IA=true` siguiendo el [quickstart.md Escenario 2](./quickstart.md) y verificar que se crea workspace + análisis completado
- [x] T016 [US4] Verificar en el frontend `http://localhost:8181` que el workspace creado por el scraper aparece en la lista `/analisis`, el dashboard renderiza correctamente y el chat responde
- [x] T017 [US4] Verificar en el frontend que la lista `/licitaciones` muestra las licitaciones scrapeadas con sus adjuntos registrados

**Checkpoint**: Demo lista — flujo completo funcional sin intervención técnica.

---

## Phase 5: Polish & Operaciones

**Purpose**: Hardening operativo para producción y onboarding.

- [x] T018 [P] Actualizar `CHANGELOG.md` con los cambios de Fase 2: automatización del scraper, Node.js en Dockerfile, nuevas variables de entorno
- [x] T019 [P] Actualizar `QUICKSTART.md` (raíz) para documentar las credenciales MP y cómo activar el scraper
- [x] T020 Agregar health-check del scraper: verificar que `docker compose logs api` no muestra errores de `node not found` o `script not found` en el primer ciclo de 30s post-arranque
- [x] T021 [P] Revisar el `ScraperBackgroundService.cs` para que en caso de error (node no encontrado, script no encontrado) cree una notificación de tipo `scraper_config_error` con mensaje descriptivo en lugar de silenciar el error

---

---

## Fase 3: Dashboard Comparativo Ejecutivo (US2)

**Goal**: Francisco (Gerente Chile) puede ver en un solo lugar cuánto vendió TIVIT vs. competidores, el ranking de frecuencia y montos, y los factores de pérdida más frecuentes — respondiendo "¿por qué Sonda vendió $80M más que nosotros?".

**Source de datos**: `analisis_resultados.contenido_json` — campos clave: `adjudicacion.ofertantes[]`, `adjudicacion.adjudicatario`, `analisis_tivit.es_ganador`, `analisis_tivit.resultado`.

---

### Phase 6: Migración SQL

- [x] T022 Crear migración `src/MPM.Api/Database/Scripts/V071__Add_usp_Analisis_ObtenerResultadosCompletos.sql` con función PostgreSQL que retorna todos los `contenido_json` de workspaces `completado` con filtro opcional de año

---

### Phase 7: Backend US2

- [x] T023 [US2] Agregar DTOs `ResultadoCompletoDto`, `DashboardEjecutivoDto`, `CompetidorRankingDto`, `LicitacionResumenEjecutivoDto` en `src/MPM.Modules.Analisis/Models/AnalisisDtos.cs`
- [x] T024 [US2] Agregar método `ObtenerResultadosCompletosAsync(int? anio)` en `src/MPM.Modules.Analisis/Data/AnalisisHandler.cs`
- [x] T025 [US2] Agregar método `GetDashboardEjecutivoAsync(int? anio)` en `src/MPM.Modules.Analisis/Services/AnalisisService.cs` con lógica de agregación C# sobre los JSON retornados
- [x] T026 [US2] Agregar endpoint `GET /api/v1/analisis/ejecutivo` en `src/MPM.Modules.Analisis/Controllers/AnalisisController.cs` con query param `?anio=`

---

### Phase 8: Frontend US2

- [x] T027 [US2] Agregar hook `useEjecutivoDashboard(anio?)` en `src/mpm-web/src/hooks/useAnalisis.ts`
- [x] T028 [US2] Crear página `src/mpm-web/src/pages/EjecutivoDashboardPage.tsx` con: KPIs globales (ganadas/perdidas/montos), ranking de competidores como tabla clickeable, tabla de últimas licitaciones con filtro por año
- [x] T029 [US2] Agregar ruta `/analisis/ejecutivo` y ítem de navegación "Ejecutivo" en `src/mpm-web/src/App.tsx`

---

### Phase 9: Verificación y Polish US2

- [x] T030 [US2] Rebuild Docker API, verificar endpoint `GET /api/v1/analisis/ejecutivo` devuelve datos con las 10 licitaciones analizadas
- [x] T031 [US2] Verificar en frontend `http://localhost:8181/analisis/ejecutivo` que el dashboard muestra datos correctos y la tabla de competidores es funcional
- [x] T032 [US2] Actualizar `CHANGELOG.md` con Fase 3

---

### Phase 10: Fase 4 — Notificaciones y Seguimiento Activo (US3)

**Goal**: Usuarios pueden seguir licitaciones activas y recibir notificaciones cuando aparecen nuevas aclaraciones detectadas por el monitor.

#### Fundación (DB + DTOs)

- [X] T033 [US3] Crear migración `V072__Create_licitaciones_seguidas_aclaraciones.sql` — tablas `licitaciones_seguidas` y `licitaciones_aclaraciones`
- [X] T034 [US3] Crear migración `V073__SP_Seguimiento_Aclaraciones.sql` — 5 stored procedures de seguimiento
- [X] T035 [US3] Extender `ApiMpService.cs` con modelos `ApiMpPreguntas` y `ApiMpAclaracion`
- [X] T036 [US3] Agregar DTOs `SeguimientoToggleDto`, `EsSeguidaDto`, `LicitacionSeguidaDto`, `LicitacionParaMonitorDto` en `LicitacionResumenDto.cs`

#### Backend

- [X] T037 [US3] Agregar constantes SP de seguimiento en `LicitacionStoredProcedures.cs`
- [X] T038 [US3] Crear `SeguimientoHandler.cs` con 5 métodos async (toggle, esSeguida, monitor, upsert, marcarNotificada, seguidas)
- [X] T039 [US3] Crear `AclaracionMonitorService.cs` — BackgroundService 30-min que detecta nuevas aclaraciones y notifica followers
- [X] T040 [US3] Agregar 3 endpoints en `LicitacionController.cs`: `POST /{codigo}/seguir`, `GET /{codigo}/seguida`, `GET /seguidas`
- [X] T041 [US3] Registrar `SeguimientoHandler` (scoped) + `AclaracionMonitorService` (hosted) en `ModuleRegistration.cs`
- [X] T042 [US3] Agregar `MONITOR_ENABLED` / `MONITOR_INTERVAL_MINUTES` a `docker-compose.yml` y `.env.example`

#### Frontend

- [X] T043 [US3] Agregar hooks `useEsSeguida`, `useSeguirToggle`, `useLicitacionesSeguidas` en `useLicitaciones.ts`
- [X] T044 [US3] Agregar columna estrella por fila en `LicitacionesTable.tsx` con `StarButtonCell`
- [X] T045 [US3] Actualizar `NotificacionesPage.tsx`: `TIPO_TAG/TIPO_LABEL` para `aclaracion_detectada` + link a licitación

#### Verificación

- [X] T046 [US3] Rebuild Docker y verificar: `docker compose up --build -d`, probar endpoints seguimiento, verificar logs del monitor

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Sin dependencias — comenzar inmediatamente
- **Phase 2 (Foundational)**: Depende de Phase 1 — **BLOQUEA** Phase 3 y 4
- **Phase 3 (US1)**: Depende de Phase 2 completada
- **Phase 4 (US4)**: Depende de Phase 3 completada (necesita al menos un análisis en DB)
- **Phase 5 (Polish)**: Depende de Phase 4 completada

### Parallel Opportunities

```bash
# Phase 1 — ejecutar en paralelo:
T001  # Verificar package.json
T002  # Verificar login.js
T003  # Crear .env.example

# Phase 2 — ejecutar en secuencia (cada paso del Dockerfile depende del anterior):
T004 → T005 → T006 → T007  # Dockerfile (secuencial)
T008  # docker-compose.yml (puede hacerse en paralelo con T004-T007)

# Phase 3 — ejecutar en paralelo (archivos distintos):
T009 + T010  # ScraperBackgroundService.cs (mismo archivo, secuencial)
T011 + T012 + T013  # Verificaciones del scraper JS (paralelas entre sí)

# Phase 5 — ejecutar en paralelo:
T018 + T019  # Documentación
T020 + T021  # Operaciones
```

---

## Implementation Strategy

### MVP (Validación mínima para demo del jueves)

1. Completar **Phase 1** (T001–T003): ~30 min
2. Completar **Phase 2** (T004–T008): ~2 horas (build y prueba del Dockerfile)
3. Completar T009–T010 de **Phase 3**: ~1 hora (ScraperBackgroundService)
4. **VALIDAR** con Escenario 1 del quickstart (scraper manual sin IA)
5. Si funciona: activar `MP_ANALISIS_IA=true` y completar T011–T013
6. **VALIDAR** con Escenario 2 y 4 del quickstart (pipeline completo + frontend)
7. Demo lista

### Orden de implementación recomendado

```
T001 T002 T003
      ↓
T004 T005 T006 T007 (T008 en paralelo)
      ↓
T009 T010
T011 T012 T013 (paralelos)
      ↓
T014 T015 T016 T017 (secuencial: validación progresiva)
      ↓
T018 T019 T020 T021
```

---

## Notes

- [P] = puede ejecutarse en paralelo (archivos distintos, sin dependencias de tareas incompletas)
- [US1] / [US4] = user story a la que pertenece la tarea
- No se incluyen unit tests (no solicitados en la spec)
- Las fases US2 y US3 de la spec se implementarán en Fase 3 y Fase 4 del SDD respectivamente
- FR-008 (credenciales con roles Admin/Analista): cubierto en Fase 0 — Auth module. No requiere tareas en Fase 2
- Commit sugerido por fase completada (no por tarea individual)
- La primera corrida del scraper con datos históricos desde 2025 puede tomar 1–4 horas; planificar fuera del horario de demo
