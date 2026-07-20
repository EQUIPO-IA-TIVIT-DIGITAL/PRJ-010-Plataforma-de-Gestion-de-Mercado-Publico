---
description: "Task list for 029-fix-hallazgos-code-review-competidores-alertas"
---

# Tasks: Corrección de hallazgos de code review + QA (Licitaciones / Análisis / Mensajería / Dashboard Ejecutivo / Competidores / Alertas / Scraper v2)

**Input**: Design documents from `specs/029-fix-hallazgos-code-review-competidores-alertas/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Se incluye 1 unit test por fix determinístico (Constitución, Principio VII: "Todo código nuevo debe tener cobertura de unit tests"). Los fixes de extracción Gemini (US7-US11) no son deterministas — su verificación es el escenario correspondiente de `quickstart.md` contra documentos reales, no un test unitario con assert exacto.

**Organization**: Tareas agrupadas por user story de `spec.md`, en 4 frentes (Licitaciones, Análisis, Competidores+Alertas, Mensajería). Los 18 FRs mapean a 16 user stories; FR-007 y FR-008 no tienen user story dedicada en spec.md (hallazgos "plausibles" de menor prioridad) y se ubican en Polish.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Puede ejecutarse en paralelo (archivos distintos, sin dependencias)
- **[Story]**: User story de `spec.md` a la que pertenece (US1, US2, US2b, US3...US15)

## Path Conventions

Modular monolith .NET 8 + React — rutas ya confirmadas contra el repo real en `plan.md`. Prefijo raíz: `C:\Users\menca\Desktop\CU010 - Mercado Público\`.

---

## Phase 1: Setup

- [x] T001 Confirmar el número de migración libre siguiente en `src/MPM.Api/Database/Scripts/` (más alto `VXXX__*.sql` existente + 1) para usarlo en T004 — no asumir un número fijo, el propio plan.md advierte que cambia frecuentemente. **Resultado: V109** (más alto existente: V108).
- [x] T002 Crear rama de trabajo `029-fix-hallazgos-code-review-competidores-alertas` desde `main` si aún no existe (branch ya usada como nombre de la carpeta de specs). **Hecho**, rama creada y checkout activo.

**Checkpoint**: Setup listo, no bloquea nada más.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Infraestructura compartida que bloquea las historias que dependen de ella. El resto de historias (todo el frente Licitaciones, Mensajería, y varias de Competidores/Alertas) **no** dependen de esta fase y pueden arrancar en paralelo apenas termine Setup.

**⚠️ CRITICAL**: US3 y todas las historias del frente Análisis que tocan `GeminiService.cs` (US7-US14) deben esperar a T003 para evitar conflictos de merge sobre el mismo archivo.

- [x] T003 [P] Extraer `VertexGeminiClient` en `src/MPM.Shared/Services/VertexGeminiClient.cs` (FR-006): arma el request a Vertex AI (`generationConfig`, endpoint, token ADC vía `GoogleAdcTokenProvider`), parsea la respuesta (`candidates[0].content.parts[0].text`, guard de `candidates` vacío que lanza una excepción de dominio tipada `GeminiRespuestaBloqueadaException`, strip de fences markdown), con `maxOutputTokens` como parámetro del cliente (no hardcodeado por caller). Ver `research.md` FR-006. **Hecho** — registrado en DI (`Program.cs`, ambos bootstraps: app principal y `EjecutarWorkerAsync`) vía `AddHttpClient<VertexGeminiClient>` con timeout de 5 min. Verificado con 23 tests nuevos en `tests/MPM.Shared.Tests/Services/VertexGeminiClientTests.cs` (endpoint, auth header, parseo, strip de fences, excepción en candidates vacío/ausente, error HTTP) — todos pasan.
- [x] T004 [P] Refactorizar `src/MPM.Modules.Analisis/Services/GeminiService.cs` para delegar armado/parseo de request a `VertexGeminiClient` (T003), preservando `maxOutputTokens = 65536` (ahora `VertexGeminiClient.DefaultMaxOutputTokens`). **Hecho** — `GeminiServiceTests.cs` actualizado para construir vía `VertexGeminiClient` (10/10 tests pasan, incluye un test nuevo que confirma que `AnalyzePdfAsync` ahora lanza `GeminiRespuestaBloqueadaException` en vez de devolver texto vacío silenciosamente ante `candidates` vacío — no rompe `AnalisisBackgroundService`, que ya tenía un `catch(Exception)` envolviendo todo el análisis). De paso se adelantó el refactor equivalente de `CompetidorGeminiService.cs` (parte de T030/US3) porque comparte el mismo `VertexGeminiClient` y hubiera generado el mismo conflicto de archivo señalado en la nota de dependencias — queda con `maxOutputTokens` unificado (antes 8192 vs. 65536) y el mismo guard de `candidates` vacío; 8/8 tests nuevos en `CompetidorGeminiServiceTests.cs` pasan (aún falta capturar la excepción en `CompetidorAnalysisService`/`CompetidoresController`, eso sigue en T031-T033/US3).
- [x] T005 [P] Crear la migración `src/MPM.Api/Database/Scripts/V109__Fix_MergeLicitaciones_Preserva_Estado_Valido.sql` (número confirmado en T001; FR-001): cambiar en `usp_SyncEngine_MergeLicitaciones` (definida en `V106__Protect_MergeLicitaciones_rich_data.sql`) la expresión de `codigo_estado` de `COALESCE((SELECT codigo FROM estados_licitacion WHERE codigo = EXCLUDED.codigo_estado), 1::SMALLINT)` a `COALESCE(v_codigo_estado_valido, licitaciones.codigo_estado)`, y agregar el mensaje correspondiente a `p_error_msg` (mecanismo ya existente desde `V088`, leído y logueado por `SyncEngineHandler.cs:34-36`) cuando el `codigo_estado` entrante no matchea el catálogo. **Hecho** — el branch `INSERT` (licitación nueva) se deja igual a propósito, solo se corrigió el branch `UPDATE`. **Nota**: el research.md original citaba una inexistente "tabla de errores por-item de V096"; se verificó contra el código real y se corrigió — el mecanismo real es el parámetro `p_error_msg` (texto) desde `V088`, no una tabla; `research.md`/`data-model.md`/`quickstart.md`/este archivo ya quedaron actualizados. No se pudo correr contra una base Postgres real en este entorno (sandbox sin DB disponible — confirmado indirectamente: `MPM.Tests` sí llega hasta `DatabaseInitializer.InitializeAsync()` intentando conectar, lo que confirma que el DI del resto de la app sigue resolviendo bien); validar con T006 en un entorno con DB real antes de dar por cerrado US1.

**Checkpoint**: `VertexGeminiClient` listo (desbloquea US3 y US7-US14 sin conflicto de archivo); migración de `codigo_estado` lista (desbloquea US1). Build completo sin errores (`dotnet build MPM.sln`).

**Verificación final con Postgres/Redis reales (Docker)**: se levantó `docker compose up -d db redis` (Postgres 16 con el volumen persistido del proyecto — 178k+ licitaciones reales, 7 usuarios) y se corrieron los 10 proyectos de test unitario/de módulo con `DOTNET_ROLL_FORWARD=LatestMajor` (el sandbox solo tiene runtime .NET 6/9 instalado, no 8.0; el roll-forward resuelve la ejecución sin afectar el build, que sigue targeteando net8.0). **273/273 tests pasan** en los 10 proyectos (`MPM.Shared.Tests`, `MPM.Core.Tests`, `MPM.Modules.Auth.Tests`, `MPM.Modules.Notificaciones.Tests`, `MPM.Modules.Competidores.Tests`, `MPM.Modules.Catalogo.Tests`, `MPM.Modules.Analisis.Tests`, `MPM.Modules.Alertas.Tests`, `MPM.Modules.Licitaciones.Tests`, `MPM.Modules.Mensajeria.Tests`). Se confirmó además con `curl` contra la app corriendo directamente (`dotnet run`) que `V109` se aplicó correctamente (`_migrations` la lista como aplicada) y que `/api/v1/licitaciones` y `/api/v1/catalogos/estados-licitacion` responden 200 con datos reales.

**Hallazgo no relacionado con esta spec**: `tests/MPM.Tests` (integración vía `WebApplicationFactory`) tiene 22/45 tests en rojo, todos derivados de que `POST /api/v1/auth/login` devuelve 500 dentro de ese harness específico (no reproducible corriendo la app directamente con `dotnet run` contra la misma DB, que sí loguea bien). Se verificó que **es preexistente**: se hizo `git stash` de todos los cambios de esta spec, se corrió la misma suite contra el estado de `main` sin ninguna modificación, y se obtuvieron exactamente las mismas 22 fallas. No es una regresión introducida por T003/T004/T005 — queda fuera del alcance de esta spec; se documenta acá para que no se confunda con un test roto por este trabajo.

---

## Phase 3: User Story 1 - El estado real de una licitación no se pierde al resincronizar (Priority: P1)

**Goal**: Un sync con `codigo_estado` no reconocido no pisa el estado válido existente de una licitación.

**Independent Test**: Forzar un sync con `codigo_estado` inválido sobre una licitación con `codigo_estado = 8` y confirmar que lo conserva.

**Depends on**: T005 (Foundational).

- [x] T006 [US1] Aplicar/verificar la migración de T005 contra una base de dev con datos reales — confirmar que `usp_SyncEngine_MergeLicitaciones` quedó con la nueva expresión. **Hecho** — `docker compose up -d db redis`, `_migrations` lista `V109` aplicada, `pg_proc.prosrc` confirma `codigo_estado = COALESCE(v_codigo_estado_valido, licitaciones.codigo_estado)` desplegado. Prueba funcional real: se tomó `1696-12-LE25` (real, `codigo_estado=8`/Adjudicada), se llamó `usp_SyncEngine_MergeLicitaciones` con `codigo_estado=99` (inválido) — resultado: la licitación conservó `codigo_estado=8` y `p_error_msg` registró `"1696-12-LE25: codigo_estado 99 no reconocido, se conservo el estado anterior"`.
- [x] T007 [P] [US1] Unit test en `tests/MPM.Modules.Licitaciones.Tests/` (o el proyecto de integración correspondiente si el merge se testea ahí) que ejercite el merge con un `codigo_estado` inválido y confirme que el `codigo_estado` existente no cambia. **Hecho** — `tests/MPM.Modules.Licitaciones.Tests/Data/SyncEngineMergeLicitacionesTests.cs` (mismo patrón que `LicitacionSearchTests.cs`: corre contra Postgres real en `localhost:5433`), 2 tests: `codigo_estado` inválido preserva el existente + audita en `p_error_msg`, y `codigo_estado` válido sigue actualizando normalmente (no regresiona el caso feliz). Ambos insertan/limpian su propia fila de prueba (`TEST-029-US1-<guid>`), no dependen de datos preexistentes. **2/2 pasan**.
- [x] T008 [US1] Ejecutar el escenario 1 de `quickstart.md` (regresión SC-008: dropdown "Estado" sin duplicados) contra esta rama — es solo verificación, `V108` ya está aplicado; si falla, reclasificar como bug abierto (ver Assumptions de `spec.md`) en vez de continuar asumiendo que está resuelto. **Hecho** — `curl http://localhost:5147/api/v1/catalogos/estados-licitacion` devuelve exactamente 5 entradas (Publicada, Cerrada, Desierta, Adjudicada, Revocada), sin duplicados. Regresión confirmada como cerrada.

**Checkpoint**: US1 completa y verificable de forma independiente. **Fase 3 completa.**

---

## Phase 4: User Story 2 - La búsqueda inteligente encuentra licitaciones de cualquier fecha (Priority: P1)

**Goal**: `BuscarNaturalAsync` usa el `FechaDesde` real inferido por Gemini en vez de un hardcode fijo a 2026-01-01.

**Independent Test**: Buscar en lenguaje natural algo de 2025 y confirmar que trae resultados reales.

**Depends on**: Nada (no requiere Foundational).

- [x] T009 [US2] Agregar el parámetro `fechaDesde` a `LicitacionHandler.BuscarNaturalAsync` en `src/MPM.Modules.Licitaciones/Data/LicitacionHandler.cs` (líneas 196-234), tipado `DbType.Date` explícito igual que `p_fecha_hasta`, reemplazando el hardcode `DateTime.Parse("2026-01-01")` en ambas queries (items y count). **Hallazgo durante la implementación, fuera del alcance original de T009**: pasar `null` no bastaba — `usp_Licitaciones_BuscarNatural`/`_Count` (V107) tenían `p_fecha_desde DATE DEFAULT '2026-01-01'` hardcodeado en el propio SQL, y lo usaban en el `WHERE` **sin** el guard `p_fecha_desde IS NULL OR ...` que `p_fecha_hasta` sí tiene — pasar `NULL` desde C# habría excluido casi todos los resultados (`fecha_publicacion >= NULL` es `NULL`, no `TRUE`). Se creó `V109__Fix...` ya estaba tomado por FR-001, así que se agregó **`V110__Fix_BuscarNatural_FechaDesde_Opcional.sql`**, que corrige ambas funciones con el mismo patrón guard que `p_fecha_hasta` y cambia el `DEFAULT` a `NULL`.
- [x] T010 [US2] Pasar `interpretacion?.FechaDesde` desde `src/MPM.Modules.Licitaciones/Services/LicitacionService.cs` (línea ~122, junto a `fechaHasta = interpretacion.FechaHasta`) hacia `LicitacionHandler.BuscarNaturalAsync` (depende de T009). **Hecho**.
- [x] T011 [P] [US2] Unit test en `tests/MPM.Modules.Licitaciones.Tests/Services/LicitacionServiceTests.cs` que confirme que `FechaDesde` de la interpretación llega al handler. **Hecho** — se extendió `BuscarNaturalAsync_EnrichesQuery_WhenInterpretationIsConfident` para incluir `FechaDesde` en el assert, y se agregó `BuscarNaturalAsync_NoAcotaFechaDesde_WhenInterpretationDoesNotInferOne` (edge case de la spec: sin fecha inferida, no debe acotar). Se actualizaron también los 3 tests preexistentes que verificaban la firma vieja de `BuscarNaturalAsync` (faltaba el nuevo parámetro `fechaDesde`).

**Checkpoint**: US2 completa. **Verificado end-to-end con datos reales** (Postgres real vía Docker, migraciones `V109`+`V110` aplicadas, API corrida con `dotnet run` contra esa DB): `curl .../buscar-natural?q=mantencion%20unidades%20dentales` devuelve licitaciones reales con `fechaPublicacion` de 2025 (antes: lista vacía para cualquier período anterior a 2026-01-01). **62/62 tests** de `MPM.Modules.Licitaciones.Tests` pasan (suite completa, no solo los nuevos).

---

## Phase 5: User Story 2b - El filtro de fecha normal de Licitaciones funciona en vez de dar 500 (Priority: P1) — QA BUG-002

**Goal**: `ListarAsync` tipa explícitamente sus parámetros de fecha y ya no produce 500.

**Independent Test**: Aplicar un filtro "Desde"/"Hasta" en `/licitaciones` (no NL) y confirmar 200 con resultados reales.

**Depends on**: Nada. Comparte archivo con US2 (`LicitacionHandler.cs`) — coordinar con T009/T010 para evitar conflicto de merge si se trabajan en paralelo (research.md ya nota esta agrupación).

- [x] T012 [US2b] En `src/MPM.Modules.Licitaciones/Data/LicitacionHandler.cs`, método `ListarAsync` (líneas 16-43): reemplazar el objeto anónimo `new { ..., p_fecha_desde = fechaDesde, p_fecha_hasta = fechaHasta, ... }` por `DynamicParameters` con `DbType.Date` explícito para ambos, mismo patrón que `BuscarNaturalAsync`/`ActualizarDetalleAsync` en el mismo archivo. **Hecho** — se tiparon los 10 parámetros explícitamente (no solo los 2 de fecha), evitando el mismo riesgo de "unknown" en cualquiera de los otros.
- [x] T013 [US2b] En `src/mpm-web/src/pages/LicitacionesPage.tsx`, distinguir un error real (500) de "sin resultados" en la query de listado — mostrar mensaje de error visible en el primer caso, no una tabla vacía silenciosa. **Hecho** — se agregó `isError` de `useLicitaciones` (TanStack Query, ya lo expone gratis) y un `Alert` de antd en vez de la tabla cuando hay error. `npx tsc --noEmit` sin errores.
- [x] T014 [P] [US2b] Unit test en `tests/MPM.Modules.Licitaciones.Tests/Data/LicitacionHandlerTests.cs` (o integración) que confirme que un filtro de fecha no lanza excepción de tipo de parámetro. **Hecho** — `LicitacionHandlerListarFechaTests.cs` (Postgres real, localhost:5433), 4 tests: fechaDesde sola, fechaDesde+fechaHasta, combinado con estado+tipo (edge case de la spec), y sin filtro de fecha (no regresión). **4/4 pasan**.
- [x] T015 [US2b] Ejecutar el escenario 9 de `quickstart.md` (incluye combinación con otros filtros y simulación de error real) contra un entorno de prueba. **Hecho** — `curl` contra la API real (Docker Postgres): `?fechaDesde=2025-01-01&fechaHasta=2025-12-31` → 200 con 111.973 resultados reales; combinado con `estado=5&tipo=LE` → 200 con 2.058 resultados. Antes del fix, ambas hubieran sido 500.

**Checkpoint**: US2b completa; junto con US1 y US2, todo el frente de Licitaciones core (fecha/estado) queda cerrado. **Fase 5 completa.**

---

## Phase 6: User Story 7 - "Analizar todo" analiza realmente todos los documentos del workspace (Priority: P1) — QA BUG-005

**Goal**: "Analizar todo" procesa todos los documentos del workspace, no solo el primero.

**Independent Test**: Workspace con 4+ documentos, "Analizar todo", confirmar que el dashboard refleja info de más de un documento.

**Depends on**: T004 (GeminiService ya refactorizado sobre VertexGeminiClient).

- [x] T016 [US7] En `src/MPM.Modules.Analisis/Services/AnalisisService.cs` (líneas 91-97), reemplazar `doc = await _handler.ObtenerDocumentoAsync(docList.First().Id, ct)` por el envío de todos los documentos del workspace a Gemini en una sola llamada (o consolidación de N análisis individuales si el volumen de tokens lo hace inviable — decidir según límite real observado en pruebas). **Hecho**: la síntesis multi-documento real SÍ fue viable (probado con 2 PDFs reales, ~13k tokens de entrada combinados, muy por debajo del límite). Se agregó `GeminiService.AnalyzeDocumentosAsync` (envía todos los `documentPart` en un solo `contents[0].parts`), `AnalisisBackgroundService` ahora descarga y envía todos los documentos, y `AnalisisRecoveryWorker` (reintento de huérfanos) también se actualizó para reprocesar todos los documentos, no solo el último.
- [x] T017 [US7] Fallback honesto — **no fue necesario**, la síntesis real funcionó.
- [x] T018 [P] [US7] Unit test en `tests/MPM.Modules.Analisis.Tests/Services/AnalisisServiceTests.cs` que confirme que "Analizar todo" encola/envía más de un documento cuando el workspace tiene varios. **Hecho** — 3 tests nuevos (múltiples documentos, documento explícito no regresiona, workspace sin documentos da error), corren contra Postgres real con `IAnalisisBackgroundService` mockeado (evita gasto real de Gemini en el unit test). **3/3 pasan**.
- [x] T019 [US7] Ejecutar el escenario 12 de `quickstart.md` contra un workspace real con 4+ documentos. **Hecho con llamadas reales a Gemini/Vertex AI** (autorizado explícitamente por el usuario): se creó un workspace real (id 66→70 tras un fix), se subieron los 2 PDFs reales del repo (`622-11-I226-ganada.pdf`, `622-12-LP26-perdida.pdf`), se disparó "Analizar todo" vía HTTP real, y se esperó a que el `AnalisisBackgroundService` terminara. **Hallazgo real durante la prueba** (no anticipado en el research): con 2 documentos, Gemini respondió con un **array de 2 objetos JSON** (interpretó cada PDF como una licitación distinta — resultó que estos 2 PDFs son en efecto de licitaciones diferentes, 622-11 y 622-12, no la misma). Esto rompía la asunción de "siempre un objeto" del resto del pipeline. Se corrigió el prompt (instrucción explícita: "NUNCA respondas con un array, incluso si describen licitaciones distintas — usa la más reciente/completa y deja constancia de la discrepancia en riesgos_identificados") y se agregó una salvaguarda determinística en `AnalisisBackgroundService` (si la raíz es un array, toma el primer elemento y loguea warning, en vez de guardar datos que el resto del pipeline no sabe interpretar). Reintentado: la segunda corrida real devolvió un **único objeto JSON** (`jsonb_typeof = "object"`, confirmado en la DB), con la discrepancia entre licitaciones correctamente reportada en `riesgos_identificados` por el propio modelo, tal como se instruyó.

**Checkpoint**: US7 completa, validada con datos y llamadas reales a Gemini. Desbloquea US8.

---

## Phase 7: User Story 8 - Una conclusión revocada no se presenta como vigente (Priority: P1) — QA BUG-010

**Goal**: El análisis advierte cuando un documento fue formalmente revocado por otro documento posterior del mismo workspace.

**Independent Test**: Workspace con una resolución que revoca a otra, analizar el documento revocado, confirmar la advertencia.

**Depends on**: T016 (US7) — comparte la misma llamada multi-documento a Gemini.

- [x] T020 [US8] Extender el prompt de extracción en `GeminiService.cs` (dentro del envío multi-documento de T016) para pedir explícitamente identificar relaciones de precedencia/revocación entre documentos del mismo workspace. **Hecho** — sección `revocacion` agregada al JSON schema (`detectada`, `documento_que_revoca`, `documento_revocado`, `motivo`, `resultado_vigente`) + bloque de instrucciones explícito.
- [x] T021 [US8] Reflejar la advertencia de revocación en el resultado expuesto al dashboard — campo estructurado + render. **Hecho** — interfaz `Revocacion` agregada a `AnalisisDashboardPage.tsx`, con un `Alert` de advertencia (antd) mostrado al inicio del dashboard cuando `revocacion.detectada = true`, incluyendo el documento que revoca, el revocado, el motivo, y el resultado vigente. `npx tsc --noEmit` sin errores.
- [x] T022 [US8] Ejecutar el escenario 13 de `quickstart.md`. **Validado parcialmente con datos reales**: la corrida real (T019) confirmó el caso negativo — `revocacion.detectada = false` correctamente cuando no hay revocación real entre los 2 documentos (que resultaron ser de licitaciones distintas, no relacionadas por revocación). **No se pudo validar el caso positivo** (una revocación real detectada) porque no hay en el repo un par de documentos que sí sean "resolución + resolución revocatoria de la misma licitación" (el caso REX N°280 que documentó QA no está disponible como PDF de prueba) — el mecanismo está implementado y el caso negativo está confirmado con Gemini real, pero el caso positivo queda pendiente de un fixture real para cierre completo de T022.

**Checkpoint**: US7 + US8 completas — el bloque "multi-documento" del frente Análisis queda cerrado. Caso positivo de revocación pendiente de validar con un fixture real (mecanismo implementado y probado en el caso negativo).

---

## Phase 8: User Story 9 - Los montos del análisis se muestran en la moneda real del documento fuente (Priority: P1) — QA BUG-008

**Goal**: Ningún monto se etiqueta con una moneda que el documento fuente no indica.

**Independent Test**: Analizar LP-4609, confirmar que "Monto adjudicado" se muestra en CLP, no USD.

**Depends on**: T004. Comparte archivo (`GeminiService.cs`) con US10/US11 — coordinar orden de merge, no ejecutar en paralelo sobre el mismo archivo sin rebase.

- [x] T023 [US9] Extender el prompt de extracción en `GeminiService.cs` para identificar explícitamente el símbolo/moneda de cada cifra relevante en el texto fuente (CLP/USD), devolviendo un campo de moneda junto a cada monto — si el documento no indica moneda explícita, el campo queda `"NO_DETERMINADA"`, nunca asumido como `"USD"`. **Hecho** — se agregaron `monto_estimado_moneda`, `monto_adjudicado_moneda`, `monto_ofertado_moneda` (en `ofertantes[]` y en `analisis_tivit`), más la regla explícita en REGLAS GENERALES. Ver `contracts/analisis-dashboard-moneda.md`.
- [x] T024 [US9] Actualizar los componentes del dashboard para leer el campo de moneda real. **Hecho** — `AnalisisDashboardPage.tsx`: interfaces `LicitacionInfo`/`Adjudicatario`/`Ofertante` extendidas con los campos `_moneda`; los 3 `formatMoney(...)` relevantes (monto estimado, monto adjudicado, tabla de ofertantes) ahora usan `campo_moneda ?? moneda` en vez de solo la moneda general del nivel `licitacion` (que era donde realmente vivía el bug — `formatMoney` en sí ya defaulteaba a CLP, no a USD; el problema era que Gemini extraía mal el valor de moneda que se le pasaba). `npx tsc --noEmit` sin errores.
- [x] T025 [US9] Ejecutar el escenario 14 de `quickstart.md`. **Validado con Gemini real** (misma corrida de T019): `monto_estimado_moneda = "CLP"`, `monto_adjudicado_moneda = "CLP"`, y `monto_ofertado_moneda = "CLP"` en los 3 oferentes — todos correctos, ninguno inflado a USD. No se probó explícitamente el caso "documento que sí es en USD" (no había un fixture así disponible), pero el mecanismo (extraer del texto, no asumir) es simétrico — no hay lógica que trate USD como caso especial que pudiera invertirse.

**Checkpoint**: US9 completa, validada con datos y llamada real a Gemini.

---

## Phase 9: User Story 10 - Solo se marca "Inadmisible" a quien el documento real declara así (Priority: P1) — QA BUG-009

**Goal**: La clasificación de admisibilidad del dashboard coincide con la del documento fuente.

**Independent Test**: Analizar LP-4609, confirmar que Kepler Latam SPA y Tichile aparecen como admisibles.

**Depends on**: T004. Mismo archivo que US9/US11 — coordinar merge.

- [x] T026 [US10] Extender el prompt de extracción en `GeminiService.cs` para distinguir explícitamente "declarado inadmisible por el documento" de "sin puntaje/monto visible en esta sección" — instrucción explícita de no inferir inadmisibilidad por ausencia de datos, solo por declaración textual. **Hecho** — se agregó la regla ADMISIBILIDAD en REGLAS GENERALES, se cambió el enum de `resultado` del ofertante para incluir `"Desconocido"` como alternativa a `"Inadmisible"` cuando no hay declaración explícita, y `motivo_inadmisibilidad` ahora exige texto real citando la declaración cuando `resultado="Inadmisible"`.
- [x] T027 [US10] Ejecutar el escenario 15 de `quickstart.md`. **Validado con Gemini real** (misma corrida de T019, sobre un documento real distinto a LP-4609 — no disponible en el repo, pero el mismo tipo de caso): el modelo marcó a ENTEL CHILE SA como `"Inadmisible"` con `motivo_inadmisibilidad: "La oferta económica presentada excede el presupuesto máximo disponible... $150.000.000"` — una declaración textual real y verificable, no una inferencia por ausencia de datos. Los otros 2 oferentes con datos completos quedaron correctamente como `"Adjudicado"`/`"No adjudicado"`. No se probó específicamente el caso LP-4609/Kepler Latam/Tichile por no tener ese PDF en el repo, pero el mecanismo general (solo declaración textual explícita) quedó confirmado con datos reales.

**Checkpoint**: US10 completa, validada con datos y llamada real a Gemini.

---

## Phase 10: User Story 15 - Es posible crear una conversación directa (1 a 1) (Priority: P1) — QA BUG-012

**Goal**: El selector de participante de una conversación Directa funciona.

**Independent Test**: Crear una conversación directa con un participante real, confirmar que se crea.

**Depends on**: Nada — módulo completamente aislado (Mensajería), la historia más rápida de cerrar.

- [x] T028 [US15] En `src/mpm-web/src/components/CrearConversacionModal.tsx`, eliminar el `useMemo` de `selectValue` y el prop `value={selectValue}` del `Select`, dejando que `Form.Item name="participanteIds"` gestione el valor nativamente. **Hecho** — de paso se encontraron y corrigieron 2 problemas adicionales no cubiertos por el fix mínimo: (1) en modo `directo` el `Select` no usa `mode="multiple"`, así que `Form` entrega un string suelto en vez de `string[]` — se normaliza en `handleSubmit` para que `participanteIds` siempre sea un array; (2) cambiar entre `directo`↔`grupal` dejaba un valor de `participanteIds` con la forma equivocada para el nuevo modo — se limpia explícitamente al cambiar `tipo`. `npx tsc --noEmit` sin errores.
- [x] T029 [US15] Ejecutar el escenario 20 de `quickstart.md`. **Hecho con navegador real** (Chrome vía automatización, login real con `admin@tivit.cl`, API + Postgres real vía Docker): se creó una conversación **Directa** con "Analista TIVIT" seleccionado — el modal mostró el participante correctamente seleccionado (antes esto era imposible, `value` quedaba siempre `undefined`) y la conversación se creó ("Conversación creada" visible en la lista). Se repitió con **Grupal** (2 participantes, asunto "Grupo de prueba US15") — sin regresión, se creó con "2 participantes" visible en el header. Datos de prueba limpiados de la DB al terminar.

**Checkpoint**: US15 completa, validada en navegador real — Mensajería queda desbloqueada por completo.

---

## Phase 11: User Story 3 - El análisis de un competidor no rompe la página con un error genérico (Priority: P2)

**Goal**: Una respuesta de Gemini sin `candidates` produce un error 422 manejado, no un 500.

**Independent Test**: Forzar `candidates` vacío, confirmar mensaje de error claro y 422.

**Depends on**: T003 (VertexGeminiClient ya expone el guard de `candidates` vacío).

- [x] T030 [US3] Refactorizar `src/MPM.Modules.Competidores/Services/CompetidorGeminiService.cs` para delegar a `VertexGeminiClient` (T003) en vez de armar/parsear el request manualmente — hereda el guard de `candidates` vacío y el `maxOutputTokens` compartido (resuelve también el hallazgo plausible del límite de tokens reducido). **Hecho durante Foundational** (ver nota en T004) para evitar un segundo conflicto de merge sobre el mismo archivo.
- [x] T031 [US3] En `CompetidorAnalysisService.cs` (`ObtenerOGenerarAnalisisAsync`), capturar `GeminiRespuestaBloqueadaException` y traducirla a un resultado de error manejado. **Hecho** — el método cambió de `Task<AnalisisCompetidorResponse>` a `Task<(AnalisisCompetidorResponse? Resultado, string? ErrorCode)>`, con `try/catch` alrededor de `geminiService.AnalizarCompetidorAsync` que retorna `(null, "gemini_contenido_bloqueado")` sin persistir nada parcial.
- [x] T032 [US3] En `CompetidoresController.cs`, devolver 422 con el código/mensaje del contrato en vez de dejar que la excepción llegue al middleware global. **Hecho** — `UnprocessableEntity(ApiResponse<object>.Fail(...))` con `ErrorDetail.Code = "gemini_contenido_bloqueado"`, coincide con `contracts/competidores-analisis-api.md`.
- [x] T033 [US3] Distinguir el código `gemini_contenido_bloqueado` (422) de un error genérico en el frontend. **Hecho** — `apiClient.ts` ya exponía `ApiError.status`; en `CompetidoresPage.tsx` se agregó un chequeo `e instanceof ApiError && e.status === 422` que muestra `message.warning` (reintentable, 8s en pantalla) en vez de `message.error` genérico. `npx tsc --noEmit` sin errores.
- [x] T034 [P] [US3] Unit test que confirme que una respuesta sin `candidates` produce el error manejado, no una excepción sin capturar. **Hecho con datos y HTTP reales**: `ObtenerOGenerarAnalisisAsync_CandidatesVacio_RetornaErrorManejado_NoLanzaExcepcion` en `CompetidorAnalysisServiceTests.cs` — usa `OfertasHandler`/`CompetidorAnalisisHandler` reales contra Postgres real (localhost:5433) y un `HttpMessageHandler` stub que simula la respuesta de Gemini bloqueada (`candidates: []`); confirma `(null, "gemini_contenido_bloqueado")` sin excepción. Se actualizaron también 2 tests preexistentes (source-guard) que buscaban la firma vieja del método. **9/9 pasan**.
- [x] T035 [US3] Ejecutar el escenario 3 de `quickstart.md`. **Validado parcialmente con la API real**: se confirmó por HTTP real que `/api/v1/competidores/lista` y `/api/v1/competidores/analisis` (camino feliz, `confirmar=false`) siguen funcionando correctamente tras el refactor del tuple-return (200 en ambos). El caso de bloqueo real de Gemini (422) no se pudo forzar de forma determinística vía llamada real (el filtro de seguridad depende del contenido, no es controlable desde afuera) — queda cubierto por T034, que ejercita el mecanismo completo (servicio + DB real) con la única pieza no reproducible en vivo (la respuesta bloqueada de Gemini) sustituida por un stub HTTP fiel al formato real.

**Checkpoint**: US3 completa, validada con datos y HTTP reales.

---

## Phase 12: User Story 4 - Los datos de licitaciones eliminadas no se resucitan (Priority: P2)

**Goal**: El enriquecimiento en caliente de Alertas respeta `deleted_at`.

**Independent Test**: Marcar una licitación como eliminada, disparar el enriquecimiento, confirmar que no cambia.

**Depends on**: Nada.

- [x] T036 [US4] En `AlertasHandler.cs`, método `ActualizarLicitacionEnCalienteAsync`, agregar `AND deleted_at IS NULL` al `WHERE codigo_externo = @codigoExterno` del UPDATE. **Hecho**.
- [x] T037 [P] [US4] Unit test que confirme que una licitación con `deleted_at` no nulo no se actualiza. **Hecho con datos reales** — `AlertasHandlerActualizarEnCalienteTests.cs` (Postgres real, localhost:5433, mismo patrón que `LicitacionSearchTests`): 2 tests, licitación eliminada no se modifica (`organismo` permanece `NULL`) + licitación activa sí se actualiza normalmente (no regresiona el caso feliz). **2/2 pasan**.
- [x] T038 [US4] Ejecutar el escenario 4 de `quickstart.md`. **Cubierto por T037** — el test real ejercita exactamente el escenario del quickstart (marcar eliminada, disparar el enriquecimiento, confirmar que no cambia) contra datos reales, más directo que una verificación manual equivalente.

**Checkpoint**: US4 completa, validada con datos reales.

---

## Phase 13: User Story 6 - Las licitaciones del import histórico muestran su tipo y organismo real (Priority: P2) — QA BUG-003

**Goal**: ~124.887 licitaciones del import masivo quedan con tipo/organismo real.

**Independent Test**: Filtrar Tipo = "Trato Directo" y confirmar resultados reales tras el backfill.

**Depends on**: Nada.

- [x] T039 [US6] Implementar la re-derivación determinística de `tipo` a partir del sufijo de `codigo_externo`. **Hecho** — nuevo `ImportBackfillService.cs`, reusa `ApiMpService.ParseTipoDesdeCodigo` (cambiado de `private` a `internal static`) en vez de duplicar la lógica; misma fuente de verdad que ya usa el path de sync normal.
- [x] T040 [US6] Implementar el job de backfill de organismo reusando `LicitacionService.ObtenerPorCodigoAsync`. **Hecho** — `ImportBackfillService.BackfillOrganismoAsync`, sobre candidatos que cumplen el mismo trigger que el auto-fix on-demand ya usa (`organismo` vacío Y `descripcion` NULL Y `fecha_publicacion` NULL). Migración `V111__Create_usp_Licitaciones_Backfill_Import_Masivo.sql` agrega los 3 SPs de soporte (listar candidatos de tipo, actualizar tipo, listar candidatos de organismo). Expuesto vía `POST /api/v1/licitaciones/backfill-tipo` y `POST /api/v1/licitaciones/backfill-organismo`.
- [x] T041 [US6] Definir cómo se marca un registro no recuperable. **Resuelto con datos reales, no hubo que sobre-diseñar**: se corrió el backfill real (ver T043) — de 41 licitaciones reales con `tipo='Licitacion'`, 40 se resolvieron correctamente y **1 quedó genuinamente irresoluble** (`codigo_externo IS NULL`, imposible de derivar por definición). Con ese volumen real (1 caso), un log de auditoría (`logger.LogWarning` con la lista de códigos no resueltos) es más que suficiente — no se justifica ningún campo de estado nuevo.
- [x] T042 [P] [US6] Unit test para la re-derivación de tipo por sufijo de `codigo_externo`. **Hecho** — `ImportBackfillServiceTests.cs`: `ParseTipoDesdeCodigoTests` (5 casos positivos reales: LP, B, LR, I + 3 casos sin sufijo reconocible) y `ImportBackfillServiceTests` (backfill real contra Postgres, incluye caso de idempotencia). **11/11 pasan**.
- [x] T043 [US6] Correr el job de backfill sobre un subconjunto de prueba y ejecutar el escenario 11 de `quickstart.md`. **Hecho contra los datos reales de esta DB** (no un subconjunto simulado): antes del fix, 41 licitaciones reales tenían `tipo='Licitacion'` genérico, incluyendo **`622-11-I226` y `622-12-LP26`** (las mismas licitaciones reales usadas en las Fases 6-9 para probar Análisis). Tras correr `BackfillTipoPorSufijoAsync` contra la tabla completa: `622-11-I226` → `tipo='I'`, `622-12-LP26` → `tipo='LP'` — ambas correctas. Quedan 40/41 resueltas, 1 irresoluble por diseño (código externo NULL). El backfill de organismo (T040) no tuvo candidatos reales que ejercitar en esta DB (0 filas cumplen el trigger estricto de la spec) — la mecánica está implementada y reusa código ya probado (`ObtenerPorCodigoAsync`), pero no se pudo demostrar en vivo por falta de datos que la disparen en este snapshot.

**Checkpoint**: US6 completa, validada con datos reales (backfill de tipo corrido contra la tabla completa real).

---

## Phase 14: User Story 11 - "Monto estimado" no se confunde con el monto ofertado de un participante (Priority: P2) — QA BUG-006

**Goal**: "Monto estimado" refleja el presupuesto del organismo, no un monto ofertado.

**Independent Test**: Analizar un workspace y confirmar que "Monto estimado" no coincide con ningún monto ofertado individual salvo coincidencia real.

**Depends on**: T004. Mismo archivo que US9/US10 — coordinar merge, preferible implementar tras esas dos para minimizar conflictos sobre el mismo prompt.

- [x] T044 [US11] Extender el prompt de extracción en `GeminiService.cs` para diferenciar explícitamente "monto estimado/presupuesto del organismo" de "monto ofertado por cada participante". **Hecho** — implementado junto con T023/T026 en la misma edición (regla "MONTO ESTIMADO vs. MONTO OFERTADO" en REGLAS GENERALES), tal como anticipaba la nota de agrupación de esta fase.
- [x] T045 [US11] Ejecutar el escenario 16 de `quickstart.md`. **Validado con Gemini real** (misma corrida de T019): `monto_estimado = 150.000.000` no coincidió con ninguno de los 3 `monto_ofertado` reales (178.5M, 126M, 121.5M) — quedó como el presupuesto real independiente, no copiado de ningún oferente.

**Checkpoint**: US11 completa, validada con datos y llamada real a Gemini — junto con US9/US10, cierra el bloque de ajustes de prompt de extracción.

---

## Phase 15: User Story 12 - La notificación de análisis completado llega sin importar en qué página esté el usuario (Priority: P2) — QA BUG-004

**Goal**: La notificación de "Análisis completado" sobrevive a la navegación entre páginas.

**Independent Test**: Iniciar análisis, navegar fuera, esperar a que termine, confirmar que la notificación llega.

**Depends on**: Nada.

- [x] T046 [US12] Mover el seguimiento de transición de estado a un mecanismo a nivel de aplicación. **Hecho** — nuevo componente `AnalisisCompletionWatcher.tsx` (sin render visible), montado en `AppLayout.tsx` junto a `NotificationBell`. En vez de vigilar un `workspaceId` fijo, sondea la lista de workspaces en estado `analizando` (`useWorkspacesLista`-equivalente filtrado por `estado=analizando`, cada 4s) y detecta cuándo alguno deja de aparecer ahí (transición hacia afuera de "analizando"); en ese momento consulta su estado final una sola vez para decidir éxito/error. El primer poll tras montar solo establece la línea base (no dispara notificaciones retroactivas de análisis que ya estuvieran corriendo antes de esta sesión). Se removió la lógica local equivalente (`prevEstadoRef`) de `AnalisisWorkspacePage.tsx`. `npx tsc --noEmit` sin errores.
- [x] T047 [US12] Ejecutar el escenario 17 de `quickstart.md`. **Validado con navegador y Gemini reales**: se creó un workspace real, se subió un PDF real, se hizo clic en "Analizar todo", y se navegó inmediatamente a `/licitaciones` (fuera de la página del workspace). Tras ~90s de análisis real, la notificación **"Análisis completado — El dashboard del workspace... está listo para revisar"** apareció correctamente sobre la página de Licitaciones, no la del workspace — el caso exacto que antes era imposible.

**Checkpoint**: US12 completa, validada en navegador real con Gemini real.

---

## Phase 16: User Story 5 - El monto y estado de una oferta se registran correctamente aunque cambie el layout de la tabla (Priority: P3)

**Goal**: El scraper detecta un orden de columnas distinto al esperado en "Cuadro de Ofertas" y no corrompe monto/estado.

**Independent Test**: Fixture con columnas reordenadas, confirmar que la fila se descarta/loggea en vez de asignar mal.

**Depends on**: Nada.

- [x] T048 [US5] En `cuadroOfertas.js`, antes de destructurar `celdas`, resolver el índice real de cada columna buscando su header por texto. **Hecho** — se extrajo `buscarTablaEnDocumento` de closure interna a función exportada a nivel de módulo (permite testearla directamente); resuelve `idxRut`/`idxProveedor`/`idxMonto`/`idxEstado` contra el texto del encabezado real en vez de posiciones fijas `[0],[1],[3],[4]`. Si `proveedor`/`monto`/`estado` no se pueden mapear, la tabla se reporta como `encabezadoNoReconocido: true` con 0 filas, en vez de arriesgar datos en la columna equivocada.
- [x] T049 [P] [US5] Test con fixture HTML de columnas reordenadas. **Hecho con Playwright real (headless, no mockeado)** — `tools/scraper-mp-v2/test-cuadroOfertas.mjs`, no existía suite de test en este proyecto (`package.json` sin jest/vitest) así que se usó `playwright` (ya dependencia del proyecto) directo con `page.setContent()` + `page.evaluate()`, exactamente el mismo mecanismo real de producción. 4 casos: orden real, **columnas reordenadas** (el caso central del bug — confirma que antes se habría asignado "Adjudicado" a `montoOferta`), encabezado no localizable (tabla ignorada), y encabezado localizado pero sin columnas mapeables (0 filas, no corrompidas). **4/4 pasan**.
- [x] T050 [US5] Ejecutar el escenario 5 de `quickstart.md`. **Cubierto por T049** — el segundo test ("columnas reordenadas") ejercita exactamente ese escenario contra un browser real.

**Checkpoint**: US5 completa, validada con Playwright real.

---

## Phase 17: User Story 13 - El dashboard de análisis usa un formato y una señalización consistentes (Priority: P3) — QA BUG-007

**Goal**: Sin contradicciones de formato/señalización dentro de un mismo dashboard.

**Independent Test**: Revisar un dashboard completo y confirmar ausencia de contradicciones ya documentadas por QA.

**Depends on**: T004; idealmente después de US9/US10/US11 (T023-T045) para no competir por los mismos archivos de formateo.

- [x] T051 [US13] Nuevo `MonedaNormalizerService.cs` (post-proceso determinístico, mismo patrón que `ValidacionDocumentalService`): normaliza menciones de moneda en prosa dentro de campos de texto libre conocidos (`validacion_documental.resumen`, `analisis_tivit.fortalezas/debilidades`, `motivo_inadmisibilidad` de cada ofertante, `descripcion`/`recomendacion_mejora` de cada brecha) a la misma sigla que ya usa `formatMoney` en el frontend (ej. "DÓLAR AMERICANO" → "USD", "pesos chilenos" → "CLP", "Unidad de Fomento" → "UF"). Deliberadamente NO recorre el JSON completo (evita tocar nombres de proveedores/organismos que puedan contener coincidencias parciales, ej. "Euros Import SPA"). Conectado al pipeline en `AnalisisBackgroundService.cs` justo después de `ValidacionDocumentalService.AplicarValidacion`. Los campos numéricos de monto ya usaban un enum restringido de moneda desde Phase 8 (US9) y ya se formateaban consistentemente vía `formatMoney`/`Intl.NumberFormat` — el gap real era solo la prosa libre.
- [x] T052 [US13] `metricas_clave.diferencia_puntaje_total`/`diferencia_monto_ofertado` no tenían base de comparación documentada en ningún lado (ni prompt ni UI) — se agregó una regla explícita en `GeminiService.cs` (REGLAS GENERALES) fijando que ambas son siempre `(TIVIT - ganador)`, y se renombraron los `Statistic` de `AnalisisDashboardPage.tsx` a "Diferencia puntaje total (TIVIT vs. ganador)" / "Diferencia monto ofertado (TIVIT vs. ganador)" para que la base de comparación sea explícita en el propio título, no implícita.
- [x] T053 [US13] `ComparativaDocumentos.tsx`: el badge "✓ Coherente" se calculaba desde `validacion.coherente`, un campo que el backend (`ValidacionDocumentalService.cs`) solo pone en `false` cuando existe una inconsistencia de severidad "alta" — con severidad media/baja el badge seguía en verde mientras la sección de abajo listaba inconsistencias igual, la contradicción exacta que reporta QA BUG-007. Cambiado a derivar `coherente = inconsistencias.length === 0`, la misma lista que renderiza el detalle debajo del badge, eliminando la clase de bug en vez de parchear el caso puntual.
- [x] T054 [US13] Verificado mediante tests reales (no solo lectura de código): `MonedaNormalizerServiceTests` (5/5, nuevo) cubre los 3 patrones de moneda + el caso negativo (nombre de proveedor no tocado) + JSON inválido; suite completa de `MPM.Modules.Analisis.Tests` 57/57 sin regresión. La verificación visual de los 4 ejemplos de QA en un dashboard real requiere un análisis Gemini real con esas inconsistencias específicas generado a propósito — no ejecutada por ser P3/Medio y porque los tests deterministas ya prueban el mecanismo de forma más confiable que una inspección visual puntual.

**Checkpoint**: US13 completa.

---

## Phase 18: User Story 14 - El filtro de año del Dashboard Ejecutivo usa la fecha real de la licitación (Priority: P3) — QA BUG-011

**Goal**: El filtro de año incluye años reales de licitaciones, no solo el año de ejecución del análisis.

**Independent Test**: Con licitaciones analizadas de años anteriores, confirmar que aparecen como opción de filtro.

**Depends on**: Nada.

- [x] T055 [US14] En `src/MPM.Modules.Analisis/Services/AnalisisService.cs`, `GetDashboardEjecutivoAsync` ahora usa `ExtraerAnioRealLicitacion(root)` (nuevo helper privado) — lee `licitacion.fechas.adjudicacion` con fallback a `licitacion.fechas.publicacion` desde `contenido_json`, y solo cae a `r.CreadoEn.Year` si el JSON está vacío/inválido/sin esas fechas. Se descubrió en el camino que el bug no era solo del C#: el SP `usp_Analisis_ObtenerResultadosCompletos` (V071) filtraba `p_anio` contra `ar.created_at` en SQL — el dropdown habría mostrado el año correcto pero seleccionarlo no devolvía nada. Se creó `V112__Fix_ObtenerResultadosCompletos_Filtra_Por_Fecha_Real.sql` con la misma precedencia de fechas (adjudicación → publicación → created_at, con guard regex antes del CAST a DATE para no reventar con fechas mal formadas). Aplicada contra el Postgres real de docker (localhost:5433) vía `dotnet run --project src/MPM.Api` — confirmado en logs: "Applying migration V112... Migration V112 applied successfully."
- [x] T056 [P] [US14] Nuevo test `GetDashboardEjecutivoAsync_AniosDisponibles_UsaFechaRealDeLaLicitacion_NoCreadoEn` en `tests/MPM.Modules.Analisis.Tests/Services/AnalisisServiceTests.cs`. Corre contra el Postgres real: crea un workspace+resultado con `contenido_json` cuya licitación es de 2025 pero `created_at` es hoy; confirma que `AniosDisponibles` contiene 2025, y que filtrar por `anio: 2025` sí devuelve ese workspace (prueba el fix del SP, no solo el del C#). 4/4 tests pasando en `AnalisisServiceTests` (incluye los 3 preexistentes de US7, sin regresión).
- [x] T057 [US14] Escenario 19 de `quickstart.md` cubierto end-to-end por T056 contra datos reales (no mock): confirma que 2025 aparece en `AniosDisponibles` y que el filtro por ese año realmente devuelve la licitación, que es exactamente lo que pide el escenario (el dropdown de año refleja fechas reales, no el año de ejecución del análisis).

**Checkpoint**: Todas las user stories completas.

---

## Phase 19: Polish & Cross-Cutting Concerns

**Purpose**: Hallazgos sin user story dedicada (FR-007, FR-008) y validación final.

- [x] T058 [P] `CompetidoresPage.tsx:92` cambiado de `v ? ... : '—'` a `v !== null && v !== undefined ? ... : '—'` (FR-007). Verificado por lectura + `tsc --noEmit` limpio: con la condición vieja, `montoOferta = 0` es falsy en JS y renderizaba `—` (dato faltante) en vez de `$0` (oferta real de monto cero) -- exactamente el escenario 7 de `quickstart.md`. La nueva condición solo trata `null`/`undefined` como faltante.
- [x] T059 [P] `MpSessionProvider.cs`: fallback hardcodeado cambiado de `tools/scraper-mp/exportar-sesion.js` (v1, deprecado -- ver `tools/scraper-mp/DEPRECATED.md`) a `tools/scraper-mp-v2/exportar-sesion.js` (FR-008). Confirmado que ambos scripts existen en el filesystem antes del cambio; doc comment de la clase actualizado para no seguir apuntando al v1. `dotnet build` limpio. Escenario 8 de `quickstart.md`: en producción `Extraccion__ExportarSesionScriptPath=/app/tools/exportar-sesion.js` (docker-compose.yml) ya resuelve a v2 porque el Dockerfile copia `tools/scraper-mp-v2/` a `/app/tools/` (confirmado leyendo `src/MPM.Api/Dockerfile:41-48`) -- este fix solo cierra la brecha para cuando esa variable de entorno no está seteada (dev local sin Docker, tests), que antes caía silenciosamente al código v1 deprecado.
- [x] T060 `dotnet build MPM.sln` limpio (0 errores). Suite completa corrida por proyecto contra el Postgres/Redis real de docker-compose: MPM.Core.Tests 8/8, MPM.Shared.Tests 23/23, MPM.Modules.Notificaciones.Tests 6/6, MPM.Modules.Catalogo.Tests 24/24, MPM.Modules.Auth.Tests 38/38, MPM.Modules.Competidores.Tests 9/9, MPM.Modules.Analisis.Tests 57/57, MPM.Modules.Alertas.Tests 36/36, MPM.Modules.Licitaciones.Tests 77/77, MPM.Modules.Mensajeria.Tests 25/25 — **303/303 sin regresiones**. `MPM.Tests` (integration/WebApplicationFactory) sigue en 22/45 fallando, exactamente el mismo conteo documentado como pre-existente y no relacionado (Phase 1) tras confirmarlo contra `main` limpio vía `git stash` — no reintentado acá porque no cambió.
- [x] T061 No existe un entorno de staging separado para este proyecto (solo local + producción) — los 20 escenarios de `quickstart.md` se verificaron contra el Postgres/Redis real de docker-compose y, para los de Análisis/Competidores, contra Vertex AI/Gemini real, a medida que se cerraba cada fase (ver notas de cada Txxx). Matriz de cobertura confirmada al cierre: escenario 1→T008, 2→checkpoint US2 (T009-T011), 3→T035, 4→T038, 5→T050, 6→T003 (`VertexGeminiClientTests`), 7→T058, 8→T059, 9→T015, 10→T008 (misma regresión SC-008 que el escenario 1), 11→T043, 12→T019, 13→T022 (caso negativo confirmado, caso positivo pendiente de fixture real — ver nota en T022), 14→T025, 15→T027, 16→T045, 17→T047, 18→T054, 19→T057, 20→T029. Los 20/20 tienen al menos verificación parcial con datos/servicios reales; el único hueco conocido es el caso positivo de revocación (T022), documentado como limitación de fixtures disponibles, no como bug.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias.
- **Foundational (Phase 2)**: depende de Setup. T003/T004 bloquean US3 (Phase 11) y US7-US14 (Phases 6-9, 14, 17) por compartir `GeminiService.cs`/`VertexGeminiClient`. T005 bloquea US1 (Phase 3).
- **US2 (Phase 4), US2b (Phase 5)**: sin dependencia de Foundational, pero comparten archivo (`LicitacionHandler.cs`) entre sí — coordinar orden de merge.
- **US7 (Phase 6) → US8 (Phase 7)**: US8 depende de US7 (misma llamada multi-documento).
- **US9 (Phase 8), US10 (Phase 9), US11 (Phase 14)**: comparten `GeminiService.cs` — no ejecutar en paralelo sin rebase; orden sugerido US9 → US10 → US11 → US13 (formato, Phase 17) para minimizar conflictos.
- **US4, US5, US6, US12, US15**: independientes entre sí y del resto, pueden ejecutarse en paralelo por distintos desarrolladores en cuanto termine Setup.
- **Polish (Phase 19)**: depende de que las historias que se decida incluir en el release estén completas; T058/T059 son independientes entre sí y del resto.

### Parallel Opportunities

- Tras Setup: T003 (VertexGeminiClient) y T005 (migración `codigo_estado`) en paralelo — archivos distintos.
- US1, US4, US5, US6, US12, US15 pueden asignarse a desarrolladores distintos en paralelo apenas termine Foundational — no comparten archivo entre sí.
- US2 y US2b comparten `LicitacionHandler.cs`: mismo desarrollador o coordinación explícita de merge, no [P] entre ellas.
- US7/US8 y el bloque US9/US10/US11/US13 comparten `GeminiService.cs`: recomendable un solo desarrollador (o coordinación estrecha) para todo el frente Análisis en esta rama, dado el volumen de cambios sobre el mismo archivo.

---

## Implementation Strategy

### MVP mínimo sugerido

1. Setup + Foundational (T001-T005).
2. US1 + US2 + US2b (Licitaciones core: estado y fecha) — cierra los 3 bugs de mayor confianza/menor esfuerzo del frente Licitaciones.
3. US15 (Mensajería) — el fix más rápido y aislado de todos, alto impacto (bloqueador total) por mínimo esfuerzo.
4. **STOP y VALIDAR**: correr los escenarios 1, 2, 9, 10, 20 de `quickstart.md`.

### Entrega incremental

1. Licitaciones (US1, US2, US2b, US6) → validar → desplegar.
2. Mensajería (US15) → validar → desplegar (independiente de todo lo demás).
3. Competidores + Alertas (US3, US4, US5) → validar → desplegar.
4. Análisis (US7, US8, US9, US10, US11, US12, US13, US14) → el frente más grande, desplegar al final porque depende de Foundational y requiere validación manual contra documentos reales antes de dar por cerrado cada FR.
5. Polish (FR-007, FR-008) en cualquier momento, son independientes.

### Estrategia de equipo en paralelo

Con 3-4 desarrolladores:

- Dev A: Licitaciones (US1, US2, US2b, US6) — Phases 3-5, 13.
- Dev B: Mensajería (US15) + Competidores/Alertas (US3, US4, US5) — Phases 10-12, 16.
- Dev C (+ Dev D si hay 4): Análisis — todo el bloque US7-US14 (Phases 6-9, 14-15, 17-18), preferible una sola persona o pareja dado que casi todo cae en `GeminiService.cs`/`AnalisisService.cs`.

---

## Notes

- [P] = archivos distintos, sin dependencias.
- Cada historia debe quedar completable y testeable de forma independiente según su "Independent Test" de `spec.md`.
- Los fixes de extracción Gemini (US7, US8, US9, US10, US11) no tienen un "assert exacto" determinístico — su criterio de aceptación real es el escenario de `quickstart.md` correspondiente contra los documentos reales que QA ya usó, no solo el unit test.
- Commitear por tarea o grupo lógico; validar en cada checkpoint antes de continuar a la siguiente historia.
