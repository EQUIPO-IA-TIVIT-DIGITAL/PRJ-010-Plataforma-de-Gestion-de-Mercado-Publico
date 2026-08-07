# Tasks: Mejora de Alertas por Correo

**Input**: Design documents from `/specs/032-mejora-alertas-correo/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/correo-alerta-formato.md, quickstart.md

**Tests**: Se incluyen tareas de test para US1 — `EvaluarMatch` ya es `internal static` y testeable sin infraestructura, y el spec define 3 escenarios de aceptación concretos que deben quedar cubiertos.

**Organization**: Tareas agrupadas por historia de usuario (US1 > US2 > US3, orden de prioridad del spec).

## Phase 1: Setup

- [X] T001 Confirmar entorno local (Docker Compose) levantado y con al menos una alerta de prueba configurable, según prerrequisitos de `quickstart.md`

## Phase 2: Foundational

Ninguna — las 3 historias son independientes entre sí (no comparten código nuevo ni bloquean una a la otra).

---

## Phase 3: User Story 1 - Alertas sin falsos positivos (Priority: P1) 🎯 MVP

**Goal**: El matching de keywords deja de disparar por coincidencias de substring dentro de otras palabras, sin romper el matching de frases multi-palabra existente.

**Independent Test**: Correr los tests unitarios de `AlertasMatchingServiceTests` contra los 3 escenarios de aceptación del spec (match real de sigla, no-match de fragmento interno, match de frase compuesta) — no requiere el resto del stack.

- [X] T002 [P] [US1] Escribir test unitario en `tests/MPM.Modules.Alertas.Tests/Services/AlertasMatchingServiceTests.cs`: keyword `"TI"` NO matchea licitación `"Producción evento mujeres participantes"`
- [X] T003 [P] [US1] Escribir test unitario en el mismo archivo: keyword `"TI"` SÍ matchea licitación `"Servicio de soporte TI para oficinas regionales"`
- [X] T004 [P] [US1] Escribir test unitario en el mismo archivo: keyword compuesta `"mesa de ayuda"` SÍ matchea licitación cuyo nombre contiene la frase completa (no-regresión de FR-002)
- [X] T005 [US1] En `src/MPM.Modules.Alertas/Services/AlertasMatchingService.cs`, método `EvaluarMatch`: reemplazar `texto.Contains(regla.p_keyword.ToLowerInvariant())` por `Regex.IsMatch(texto, $@"\b{Regex.Escape(regla.p_keyword)}\b", RegexOptions.IgnoreCase)` (agregar `using System.Text.RegularExpressions;`)
- [X] T006 [US1] En el mismo método, aplicar el mismo cambio a la comparación de sinónimos IA (`sinonimos.FirstOrDefault(s => texto.Contains(s.ToLowerInvariant()))` → equivalente con `Regex.IsMatch` y límites de palabra)
- [X] T007 [US1] Correr `dotnet test tests/MPM.Modules.Alertas.Tests --filter "FullyQualifiedName~AlertasMatchingService"` y confirmar que T002-T004 pasan, junto con los tests preexistentes del archivo (sin regresiones)

**Checkpoint**: US1 completamente funcional y verificable de forma aislada — el MVP de esta mejora.

---

## Phase 4: User Story 2 - Correo de alerta más informativo (Priority: P2)

**Goal**: El correo de alerta incluye organismo, fecha de cierre y enlace directo cuando estén disponibles, sin romper el envío cuando falten.

**Independent Test**: Disparar `POST /api/v1/alertas/{id}/probar` sobre una licitación real con los 3 campos poblados y verificar el HTML del correo contra `contracts/correo-alerta-formato.md`; repetir con una licitación sin fecha de cierre y confirmar que el correo se sigue enviando, solo omitiendo esa línea.

- [X] T008 [US2] Crear migración `src/MPM.Api/Database/Scripts/V129__Ampliar_UspLicitacionesListarParaMatching.sql` que redefine `usp_Licitaciones_ListarParaMatching` agregando `fecha_cierre` y `link` al `SELECT` (mismo `CREATE OR REPLACE FUNCTION`, sin cambiar la firma de entrada)
- [X] T009 [P] [US2] En `src/MPM.Modules.Alertas/Models/AlertasDtos.cs`, agregar `DateTime? FechaCierre` y `string? Link` al record `LicitacionParaMatching`
- [X] T010 [US2] En `src/MPM.Modules.Licitaciones/Data/LicitacionHandler.cs`: agregar `p_fecha_cierre`/`p_link` a `MatchingRow` y propagarlos en la construcción de `LicitacionParaMatching` dentro de `ListarParaMatchingAsync`
- [X] T011 [US2] En `src/MPM.Modules.Alertas/Services/EmailNotificationService.cs`, ampliar la firma de `EnviarAsync` con `string? organismo, DateTime? fechaCierre, string? link` y actualizar el HTML según `contracts/correo-alerta-formato.md` (cada campo se omite si es null/vacío, `WebUtility.HtmlEncode` en organismo, formato `dd-MM-yyyy` en fecha)
- [X] T012 [US2] En `src/MPM.Modules.Alertas/Services/AlertasMatchingService.cs`, método `ProcesarGrupoAsync`: pasar `licitacion.Organismo, licitacion.FechaCierre, licitacion.Link` en la llamada a `email.EnviarAsync`
- [X] T013 [US2] Validar en Docker local siguiendo `quickstart.md` US2: correo con los 3 campos presentes, y correo con `fechaCierre` ausente (sin texto roto)

**Checkpoint**: US2 funcional de forma independiente, sobre la base de US1 ya mergeada (no depende de ella técnicamente, pero se implementa después por prioridad).

---

## Phase 5: User Story 3 - Horario de envío alineado a la jornada laboral (Priority: P3)

**Goal**: El primer envío diario de alertas ocurre a las 8am hora de Santiago, no a las 3am.

**Independent Test**: `gcloud scheduler jobs describe sync-job-scheduler` devuelve `schedule: 0 8,15 * * *` — no requiere esperar un disparo real.

- [ ] T014 [US3] Ejecutar `gcloud scheduler jobs update <tipo> sync-job-scheduler --project=tivit-cu010 --location=us-central1 --schedule="0 8,15 * * *"` (acción de infraestructura sobre prod real — requiere confirmación explícita del usuario antes de ejecutar, mismo criterio que otros cambios de infraestructura de esta sesión)
- [ ] T015 [US3] Verificar con `gcloud scheduler jobs describe sync-job-scheduler --project=tivit-cu010 --location=us-central1 --format="value(schedule)"` que devuelve `0 8,15 * * *`

**Checkpoint**: US3 completo — cambio de configuración puro, sin código.

---

## Phase 6: Polish & Cross-Cutting

- [X] T016 Correr la suite completa de `dotnet test tests/MPM.Modules.Alertas.Tests` y `tests/MPM.Modules.Licitaciones.Tests` para confirmar cero regresiones fuera de los archivos tocados
- [X] T017 Actualizar `CHANGELOG.md` con un resumen de las 3 mejoras bajo `[Unreleased]`

## Dependencies & Execution Order

- **US1 (P1)** no depende de nada — puede implementarse y mergearse primero, es el MVP.
- **US2 (P2)** es independiente de US1 en código (archivos distintos), pero se secuencia después por prioridad de negocio. T008 (migración) debe aplicarse antes de T010 (que asume las columnas ya vienen del `SELECT`).
- **US3 (P3)** es 100% independiente de US1 y US2 — es un cambio de infraestructura que puede aplicarse en cualquier momento, incluso antes que el código, sin ningún acoplamiento.
- **Polish (T016-T017)** después de que US1 y US2 estén implementadas.

## Parallel Example

Dentro de US1, T002/T003/T004 (los 3 tests) pueden escribirse en paralelo al ser el mismo archivo pero bloques de test independientes sin dependencias entre sí; T005/T006 deben ir después (implementación que los tests van a ejercitar).

Dentro de US2, T009 (record) puede hacerse en paralelo a T008 (migración SQL) — son archivos y capas distintas — pero ambas deben completarse antes de T010.

## Implementation Strategy

**MVP = US1 solamente**: corrige el problema más grave reportado (ruido que hace que el usuario ignore las alertas) con el cambio de menor riesgo (un método, sin tocar SQL ni infraestructura). Se puede desplegar y validar en producción de forma aislada antes de continuar con US2/US3.

**Incremental**: US2 se agrega después, ampliando la fuente de datos y el formato de correo sin tocar el matching ya corregido. US3 se puede aplicar en paralelo a cualquiera de las dos anteriores, en el momento que el usuario confirme el cambio de horario en Cloud Scheduler.
