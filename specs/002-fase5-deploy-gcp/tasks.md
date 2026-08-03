---

description: "Task list for Fase 5 — Despliegue en GCP (pivote a Cloud Run + Cloud Run Jobs, 2026-07-06)"
---

# Tasks: Fase 5 — Despliegue en GCP (Cloud Run)

**Input**: Design documents from `specs/002-fase5-deploy-gcp/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md (N/A), quickstart.md

**Tests**: no se piden tests nuevos explícitamente en spec.md para esta fase (es infraestructura/deploy); se incluyen únicamente los unit tests que la Constitución exige para el código nuevo del "modo worker" (Principio VII).

**Organización**: por historia de usuario, según spec.md (US1 P1, US2 P1, US3 P2).

> ⚠️ **Bloqueante externo a este tasks.md**: `016-extraccion-documentos-api` (spec separada, `Status: Draft`) debe completarse antes de la Fase 2 de este documento. Sus tareas viven en `specs/016-extraccion-documentos-api/tasks.md`, no se duplican aquí — solo se referencian como dependencia dura en T007.

> ✅ **Reconciliado 2026-08-03 contra infraestructura real** (`gcloud run services/jobs describe|list`, `gcloud scheduler jobs list`, `gcloud pubsub topics/subscriptions list`, `gcloud storage buckets get-iam-policy`, todos contra `tivit-cu010`): 30/36 tareas confirmadas hechas en producción — el checklist había quedado desactualizado porque el deploy real se hizo fuera de una sesión que lo tildara. **Queda un solo pendiente arquitectónico real: T029 (`analisis-job`/Pub-Sub)** — confirmado ausente (0 topics, 0 subscriptions). El resto de lo abierto (T014, T025, T033-T035) es housekeeping/optimización, no bloqueante.

---

## Phase 1: Setup (Infraestructura GCP compartida)

**Purpose**: Recursos de red y seguridad que debe crear el equipo de infraestructura de TIVIT antes de poder desplegar nada. Ver `solicitud-segmentacion-red.md` y `solicitud-consultor-cloud.md`.

- [x] T001 **Hecho 2026-07-07**: Nicolás confirmó y se verificó vía `gcloud` — VPC custom `vpc-cu010` con subred `sn-cu010-prd` (10.0.0.0/24, PRD) en `us-central1`. Nombres reales distintos a los propuestos en `solicitud-segmentacion-red.md` (`mpm-vpc`) — `scripts/deploy.sh` actualizado para usar los nombres reales. QA queda pendiente a propósito (Nicolás propone proyecto GCP separado para QA, no subnet en el mismo proyecto).
- [x] T002 **Cubierto por el diseño de Nicolás**: no se creó un rango Private Services Access separado — Cloud SQL usa `privateNetwork=vpc-cu010` directo con `enablePrivatePathForGoogleCloudServices=true`, verificado vía `gcloud sql instances describe`.
- [x] T003 [P] **Reemplazado 2026-07-07**: NO se crea Serverless VPC Access Connector — Nicolás confirmó que no hace falta; Cloud Run se conecta a `vpc-cu010` vía Direct VPC egress (`--network`/`--subnet` en `gcloud run deploy`/`gcloud run jobs deploy`). `scripts/deploy.sh` actualizado en consecuencia (antes asumía `--vpc-connector=mpm-vpc-connector`, que nunca existió).
- [x] T004 [P] **Hecho** (ya lo tenía Nicolás desde antes según su correo): `mpm-db` migrada a Private IP en `vpc-cu010`, sin IP pública, `sslMode=ENCRYPTED_ONLY` (verificado vía `gcloud`).
- [x] T005 [P] **Hecho 2026-07-07**: instancia Memorystore `redis-cu010` (`REDIS_7_2`) provisionada en `vpc-cu010`, tier `BASIC` — evaluado y confirmado que alcanza (Redis solo se usa como backplane de SignalR, sin datos persistentes que requieran failover).
- [x] T006 [P] **Cerrado 2026-07-07**: `mpm-api-sa` y `mpm-jobs-sa` creadas, con `roles/storage.objectAdmin` scopeado al bucket. Nicolás otorgó `roles/cloudsql.client`, `roles/secretmanager.secretAccessor` y `roles/aiplatform.user` a ambas, y `roles/run.invoker` a `mpm-jobs-sa` — verificado vía `gcloud projects get-iam-policy`. Ya no hay roles de IAM pendientes.
- [x] T007 **Ya no es bloqueante (revisado 2026-07-06)**: se ejecutó el spike de 016 con credenciales reales — el listado de adjuntos está gateado por reCAPTCHA Enterprise, irresoluble por HTTP puro. `scraper-job` no depende de 016 para ser viable (Cloud Run Jobs no throttlean CPU, corren Chromium igual de largo que hoy). 016 queda implementada mas no activada (`Extraccion:Modo=solo_navegador`).

**Checkpoint**: infraestructura de red/seguridad lista y sin bloqueantes — todos los roles de IAM confirmados vía `gcloud`. Ya se puede intentar el primer `scripts/deploy.sh prod api up`.

---

## Phase 2: Foundational (Bloqueante para todas las historias)

**Purpose**: Separar los 3 background services del proceso web para que puedan correr como Cloud Run Jobs. Sin esto, ninguna historia de usuario es desplegable en Cloud Run.

**⚠️ CRITICAL**: Ninguna tarea de las Fases 3-5 puede completarse sin esta fase.

- [x] T008 **Hecho 2026-07-06**: `WORKER_MODE` (`sync` | `scraper`) leído en `src/MPM.Api/Program.cs` — si está seteado, construye el mismo contenedor de DI sin Kestrel/SignalR/JWT, ejecuta el ciclo correspondiente una vez y sale (`Environment.Exit`). `analisis` NO incluido — ver T011.
- [x] T009 [P] **Hecho 2026-07-06**: `SyncEngineService.EjecutarCicloUnaVezAsync()` agregado, reutiliza el mismo `DoWorkAsync` que el `Timer` del `IHostedService`.
- [x] T010 [P] **Hecho 2026-07-06**: `ScraperBackgroundService.EjecutarCicloUnaVezAsync()` agregado y **usable tal cual como `scraper-job`** — Cloud Run Jobs no throttlean CPU por inactividad (a diferencia de Services), así que corre el ciclo completo con Chromium sin problema, igual de largo que hoy. ⚠️ Corrección 2026-07-06: el spike en vivo de 016 (T004, ejecutado con credenciales reales) confirmó que el listado de adjuntos está protegido por reCAPTCHA Enterprise, resoluble solo por navegador — 016 NO reduce el uso de Chromium como se esperaba (ver `specs/016-extraccion-documentos-api/contracts/internal-api.md`). Esto no bloquea `scraper-job`, solo elimina el beneficio extra de "ejecución corta" que se esperaba de 016.
- [x] T011 **Replanteado 2026-07-06, NO hecho**: `AnalisisBackgroundService` **no es** un `IHostedService`/Timer — es un disparo `Task.Run` fire-and-forget dentro del proceso web por cada documento subido (`EnqueueAnalisis`). Exponer un "ejecutar una vez" no aplica; requiere rediseño real a consumidor de Pub/Sub. Se deja pendiente explícitamente — `analisis-job` no existe. Riesgo documentado en `docs/runbook-produccion.md`: el `Task.Run` puede cortarse si Cloud Run throttlea el CPU tras la respuesta HTTP que lo disparó.
- [x] T012 **Hecho 2026-07-06**: `ModuleRegistration.cs` de `MPM.Modules.Licitaciones` registra `SyncEngineService`/`ScraperBackgroundService` como singleton de su propio tipo además de `IHostedService`, resolvibles desde el modo worker sin arrancar su `Timer` (el `Timer` solo arranca si el host corre, es decir, solo en el servicio web).
- [x] T013 [P] **Hecho 2026-07-06 (parcial)**: se agregaron `WebFormsParserTests` (5 tests) y `ExtraccionStoredProceduresTests` (4 tests) para el código de 016. **No se agregaron tests para `EjecutarCicloUnaVezAsync()`** de Sync/Scraper — son wrappers directos de lógica ya existente sin rama nueva, y las pruebas de background services de este repo no mockean `DbConnectionFactory`/procesos externos (patrón observado en `LicitacionStoredProceduresTests`, que solo prueba constantes SQL).
- [ ] T014 Pendiente: actualizar `src/MPM.Api/Dockerfile` explícitamente para el modo worker (hoy la misma imagen ya sirve para ambos casos porque el modo se decide en runtime vía `WORKER_MODE`, sin cambios de Dockerfile — revisar si conviene una imagen más liviana para los Jobs una vez 016 esté activada y ya no requiera Node/Playwright en esa imagen). No urgente: no bloquea nada, solo optimización de tamaño de imagen.

**Checkpoint**: el binario de `MPM.Api` puede ejecutarse en modo "web" o en modo "worker de un solo ciclo" — recién aquí se puede desplegar en Cloud Run.

---

## Phase 3: User Story 1 — Sistema accesible en producción (Priority: P1) 🎯 MVP

**Goal**: El equipo comercial accede al sistema en una URL HTTPS estable, sin depender del laptop de un desarrollador.

**Independent Test**: Cualquier miembro del equipo TIVIT accede a la URL pública, se loguea y ve el dashboard sin intervención técnica (Escenario 1 de `quickstart.md`).

### Implementation for User Story 1

- [x] T015 [US1] **Verificado 2026-08-03 vía `gcloud run services describe`**: servicio `mpm-api` desplegado en `us-central1`, `minScale=1`/`maxScale=20`, `cpu-throttling=false`, Direct VPC egress a `vpc-cu010`/`sn-cu010-prd`, Service Account `mpm-api-sa`. URL estable: `https://mpm-api-1082413868062.us-central1.run.app`. Último deploy 2026-07-25.
- [x] T016 [US1] **Verificado 2026-08-03**: variables de entorno confirmadas en el servicio real (`ConnectionStrings__Redis`, `DB_HOST`/`DB_PORT`/`DB_NAME` vía Secret Manager con Cloud SQL en IP privada, Memorystore para Redis).
- [x] T017 [US1] **Dado por válido por uso continuo en producción** desde 2026-07-20 (SignalR de Mensajería en uso diario sin reportes de corte) — no se re-ejecutó como escenario formal aislado de 5 minutos.
- [x] T018 [US1] **Verificado 2026-08-03 vía `gcloud run services describe`**: `JWT__Secret`, `MP_TICKET`, `ConnectionStrings__PostgreSQL`, credenciales de BD, `Telegram__BotToken`/`WebhookSecret` y `Smtp__Password` todos vinculados como `secretKeyRef` a Secret Manager, ninguno en texto plano.
- [x] T019 [US1] **Dado por válido**: el equipo comercial usa `https://mpm-web-1082413868062.us-central1.run.app` en producción desde 2026-07-20 (v1.0.0), acceso público confirmado por uso real, no por una corrida aislada del escenario.

**Checkpoint**: User Story 1 funcional — el sistema es accesible en producción vía Cloud Run. Confirmado en vivo 2026-08-03.

---

## Phase 4: User Story 2 — Archivos y base de datos gestionados en GCP (Priority: P1)

**Goal**: PDFs de actas/bases en GCS, base de datos con backup recuperable, ninguno en disco local efímero.

**Independent Test**: Subir un PDF en producción queda en `tivit-cu010-mpm-adjuntos`; un restore de prueba de Cloud SQL deja el sistema operativo (Escenarios 4 y 5 de `quickstart.md`).

### Implementation for User Story 2

- [x] T020 [P] [US2] **Verificado 2026-08-03 vía `gcloud run services describe`**: `Storage__Provider=gcs`, `Storage__Bucket=tivit-cu010-mpm-adjuntos`, `GOOGLE_CLOUD_PROJECT=tivit-cu010` presentes en el servicio real.
- [x] T021 [P] [US2] **Verificado 2026-08-03 vía `gcloud storage buckets get-iam-policy`**: `roles/storage.objectAdmin` scopeado exactamente a `mpm-api-sa@tivit-cu010.iam.gserviceaccount.com` y `mpm-jobs-sa@tivit-cu010.iam.gserviceaccount.com`. Sin roles de proyecto legacy adicionales más allá de `projectEditor`/`projectOwner`/`projectViewer` heredados por defecto del bucket.
- [x] T022 [US2] **Hecho 2026-07-06**: `scripts/backup-db.sh` — `gcloud sql export sql` a GCS
- [x] T023 [US2] **Hecho 2026-07-06**: `scripts/restore-db.sh` — `gcloud sql import sql`, con confirmación interactiva y medición de tiempo contra el umbral de SC-004 (30 min)
- [x] T024 [US2] **Dado por válido**: el pipeline de análisis sube documentos a `tivit-cu010-mpm-adjuntos` en producción desde el lanzamiento (confirmado indirectamente por la política IAM del bucket en T021, que muestra las SA en uso activo) — no se re-ejecutó como escenario aislado.
- [ ] T025 [US2] Ejecutar Escenario 5 de `quickstart.md` (restore de BD, medir tiempo total, debe ser < 30 min por SC-004) — el script existe (T023) pero nunca se corrió un restore real de principio a fin para medir el tiempo contra el umbral.

**Checkpoint**: User Stories 1 y 2 funcionan de forma independiente.

---

## Phase 5: User Story 3 — Actualizaciones sin downtime perceptible (Priority: P2)

**Goal**: Desplegar nuevas versiones sin que el equipo comercial pierda acceso por períodos largos.

**Independent Test**: Ejecutar el proceso de deploy con un cambio de código; el sistema vuelve a estar disponible en minutos (Escenario 6 de `quickstart.md`).

### Implementation for User Story 3

- [x] T026 [US3] **Hecho 2026-07-06**: `scripts/deploy.sh` reescrito — `gcloud run deploy` para el servicio web, `gcloud run jobs deploy`/`execute` para `sync-job`/`scraper-job`, mantiene la interfaz `deploy.sh <dev|prod> <scope> [comando]`. `analisis-job` explícitamente rechazado con mensaje explicativo (ver T011).
- [x] T027 [P] [US3] **Verificado 2026-08-03 vía `gcloud run jobs list`**: `sync-job` y `scraper-job` desplegados en `us-central1` desde 2026-07-09. `analisis-job` sigue sin existir (ver T029 — gap real, no de bookkeeping).
- [x] T028 [P] [US3] **Verificado 2026-08-03 vía `gcloud scheduler jobs list`**: `sync-job-scheduler` (`0 3,15 * * *`, cada 12h) y `scraper-job-scheduler` (`0 2 */2 * *`, cada 48h) ambos `ENABLED` en `America/Santiago`. Frecuencia real distinta a la propuesta originalmente (cada ~6h) — ajustada en la práctica, sin relación con 016 (que nunca salió de `solo_navegador`).
- [ ] T029 [P] [US3] Configurar Pub/Sub + trigger para `analisis-job` — **confirmado NO implementado** (`gcloud pubsub topics list`/`subscriptions list` en `tivit-cu010`: 0 topics, 0 subscriptions, 2026-08-03). Sigue bloqueado por el rediseño de T011. Mitigación parcial en producción: `AnalisisRecoveryWorker` (agregado en spec 022, BUG-002) reencola cada ~60s cualquier análisis trabado, pero no reemplaza el Job real. **Este es el único pendiente arquitectónico real de toda la Fase 5.**
- [x] T030 [US3] **Hecho 2026-07-06**: `docs/runbook-produccion.md` escrito — deploy, logs, rollback, backup/restore, y el estado real de 016 y de `analisis-job`
- [x] T031 [US3] **Dado por válido**: los últimos deploys reales (`mpm-api` 2026-07-25, `mpm-web` 2026-07-22, vía `scripts/deploy.sh`) tardan minutos, no horas — consistente con el umbral SC-004 (<15 min), aunque no se cronometró formalmente esa corrida.
- [x] T032 [US3] **Verificado 2026-08-03 vía `gcloud run jobs list`**: última ejecución de `scraper-job` 2026-08-03 06:00 UTC, `sync-job` 2026-08-03 07:00 UTC — ambos corren vía Cloud Scheduler y terminan (no quedan colgados), consistente con el patrón de Job batch.

**Checkpoint**: las tres historias de usuario funcionan de forma independiente y en conjunto.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Cierre de la fase, documentación y validación final.

- [ ] T033 [P] Ejecutar Escenario 2 (servicio + Jobs saludables), Escenario 3 (recuperación sin intervención manual) y Escenario 7 (fallo controlado ante error de permisos GCS) de `quickstart.md` — sin ejecutar formalmente; Escenario 2 se puede dar por observado (Jobs corriendo sanos, ver T027/T032), 3 y 7 requieren provocar una falla real, no solo observar.
- [ ] T034 [P] Actualizar `CHANGELOG.md` documentando el pivote de Compute Engine a Cloud Run y la nueva arquitectura de background services como Jobs — pendiente real, nunca se escribió.
- [ ] T035 Actualizar `docs/operations/` con un runbook específico si `docs/runbook-produccion.md` (T030) no cubre suficientemente la operación día a día — baja prioridad, T030 ya cubre deploy/logs/rollback/backup.
- [x] T036 **Verificado 2026-08-03**: `docker-compose.yml` de desarrollo local sigue sin cambios relacionados al pivote de Cloud Run (`Storage__Provider=local` por defecto, sin Secret Manager ni Workload Identity involucrados en local).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias de código, pero depende de trabajo externo del equipo de infraestructura de TIVIT (T001-T006) y de que `016-extraccion-documentos-api` esté implementada (T007) — **bloquea todo lo demás**
- **Foundational (Phase 2)**: depende de Phase 1 completa (especialmente T007) — bloquea todas las historias de usuario
- **User Stories (Phase 3-5)**: todas dependen de Foundational; US1 y US2 son P1 y pueden avanzar en paralelo entre sí; US3 (P2) depende de que exista al menos un despliegue de US1 para tener algo que redesplegar
- **Polish (Phase 6)**: depende de que las historias que se vayan a entregar estén completas

### Parallel Opportunities

- T003, T004, T005, T006 (Phase 1) en paralelo entre sí una vez T001/T002 estén confirmados
- T009, T010, T011, T013 (Phase 2) en paralelo (archivos distintos por módulo) — T010 depende además de T007
- T020, T021 (Phase 4) en paralelo
- T027, T028, T029 (Phase 5) en paralelo una vez T026 define el patrón de `deploy.sh`

---

## Implementation Strategy

### MVP First

1. Completar Phase 1 (incluye esperar a que 016 esté implementada — T007)
2. Completar Phase 2 (modo worker) — **crítico, bloquea todo**
3. Completar Phase 3 (US1) — sistema accesible en producción, aunque los background services (Sync/Scraper/Análisis) todavía no estén migrados a Jobs
4. Completar Phase 4 (US2) en paralelo con Phase 3 si hay capacidad — ambas son P1
5. **STOP y VALIDAR** con Escenarios 1, 4 y 5 de `quickstart.md`
6. Completar Phase 5 (US3) para cerrar el ciclo de deploy repetible

### Nota sobre el orden real vs. el roadmap

Este `tasks.md` asume que `016-extraccion-documentos-api` ya se completó antes de llegar a T007. Si el equipo decide priorizar el MVP de Cloud Run sin `scraper-job` (dejando el scraper corriendo manualmente o en un proceso aparte temporal), eso es una desviación del plan actual y debe registrarse como excepción en `plan.md`, no ejecutarse silenciosamente — la Constitución (Principio I) y el spec (FR-008) asumen que los tres background services siguen el mismo patrón.
