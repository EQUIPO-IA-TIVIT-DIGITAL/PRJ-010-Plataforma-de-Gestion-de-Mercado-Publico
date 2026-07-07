# Implementation Plan: Fase 5 — Despliegue en GCP

**Branch**: `002-fase5-deploy-gcp` | **Status**: REPLANIFICADO 2026-07-06 — pivote a Cloud Run, bloqueado por dependencia de `016-extraccion-documentos-api`
**Spec**: [spec.md](./spec.md) | **Semana**: N1 — ya no es "inmediata sin condiciones": requiere 016 primero
**Actualizado**: 2026-07-06 — se descarta Compute Engine, se adopta Cloud Run + Cloud Run Jobs (ver spec.md "Actualización 2026-07-06")

> **Bloqueante actual**: no se puede implementar esta fase hasta que `016-extraccion-documentos-api` esté implementada (hoy `Status: Draft`, 0/21 tareas). Sin eso, `ScraperBackgroundService` no puede empaquetarse como Cloud Run Job. Ver detalle en spec.md y research.md.

---

## Summary

Llevar el sistema MPM de Docker local a producción en GCP, en **Cloud Run** (no Compute Engine — pivote 2026-07-06). Reutiliza infraestructura GCP ya provisionada: proyecto `tivit-cu010` y bucket GCS `tivit-cu010-mpm-adjuntos` (visibles en `docker-compose.yml` y el commit `62c5bf2 fix(env): centralize config in .env and migrate storage to GCS`). `GcsStorageService` ya implementa `IStorageService` — no hace falta escribir un adaptador de storage nuevo. La novedad de este replanteo es que el cómputo deja de ser una VM persistente: el servicio web (API + frontend) corre en Cloud Run, y los tres background services de larga duración se separan en Cloud Run Jobs disparados por Cloud Scheduler/Pub-Sub — lo cual requiere primero haber implementado `016-extraccion-documentos-api` para que el scraper deje de depender de un navegador corriendo de forma continua.

---

## Technical Context

**Plataforma**: Google Cloud Platform, proyecto `tivit-cu010`
**Cómputo**: **Cloud Run** (servicio web, API + frontend) — pivote 2026-07-06, reemplaza a Compute Engine. Ver research.md §1 (revisado) para el detalle de por qué antes se había descartado y qué cambió.
**Background services**: **Cloud Run Jobs**, uno por servicio (`sync-job`, `scraper-job`, `analisis-job`), disparados por Cloud Scheduler (Sync diario, Scraper cada ~6h) o Pub/Sub (Análisis, event-driven por cada solicitud de análisis en vez de polling). Requiere haber separado estos tres `IHostedService` del proceso del API — cambio de código, no solo de infraestructura (ver "Cambios de código requeridos").
**Base de datos**: Cloud SQL para PostgreSQL, ya existe (`mpm-db`). **Private IP obligatorio** (sin IP pública) — requiere Serverless VPC Access Connector para que Cloud Run llegue a la IP privada.
**Storage**: GCS, bucket `tivit-cu010-mpm-adjuntos` ya existente — reutiliza `GcsStorageService`
**Redis**: Memorystore for Redis (revisado 2026-07-06) — Cloud Run no sostiene un contenedor Redis persistente como una VM; Memorystore requiere el mismo Serverless VPC Access Connector que Cloud SQL (misma VPC).
**Red (VPC)**: VPC custom por ambiente (no la default del proyecto), con Serverless VPC Access Connector para que los servicios/Jobs de Cloud Run lleguen a Cloud SQL y Memorystore por IP privada. Ver `research.md` §5b.
**TLS / exposición pública**: Cloud Run expone HTTPS gestionado automáticamente en la URL `*.run.app` por defecto — no requiere Load Balancer ni certbot para tener el sistema accesible. Se puede mapear un dominio propio más adelante (`gcloud run domain-mappings`) cuando exista, sin bloquear el despliegue inicial.
**Gemini API**: Llamadas directas desde Cloud Run a `generativelanguage.googleapis.com` (sin cambios, sin necesidad de la VPC privada — es tráfico saliente a internet, no a un recurso interno).
**Dependencia dura**: `016-extraccion-documentos-api` debe estar implementada antes de poder crear `scraper-job` (ver spec.md).
**Estimación**: revisada al alza — 1 semana original ya no aplica; incluye completar 016 (estimación propia en su spec) + separar 3 background services en Jobs + reconfigurar red/Redis. **Complejidad**: Alta.

---

## Cambios de código requeridos

### 1. Separar los 3 background services en entry points ejecutables como Cloud Run Job (NUEVO 2026-07-06)

Hoy `SyncEngineService`, `ScraperBackgroundService` y `AnalisisBackgroundService` son `IHostedService` registrados en `MPM.Api` (`Program.cs`) y corren embebidos en el mismo proceso que sirve HTTP. Un Cloud Run Job no sirve requests — ejecuta un comando y termina. Se necesita:

```text
src/MPM.Api/Program.cs                          ← Agregar un modo "worker" (flag/env var, ej. WORKER_MODE=sync|scraper|analisis)
                                                    que, en vez de levantar Kestrel, ejecuta el ciclo del servicio
                                                    correspondiente una vez y termina (Environment.Exit al finalizar).
                                                    No se extrae a proyectos .csproj nuevos — mismo binario, distinto
                                                    entry point, para no violar el Principio I (Modular Monolith) ni
                                                    duplicar registro de DI.
```

Cada uno de los tres servicios ya es queue/DB-driven (Sync verifica última sync, Análisis procesa una cola, Scraper ya corre `--daemon --incremental` como proceso Node separado) — el cambio es de "loop infinito con Timer" a "ejecutar un ciclo y salir", que es justamente el modelo `--incremental` que el scraper Node ya soporta.

### 2. Configuración de producción para GCS (sin adaptador nuevo)

`GcsStorageService` ya existe — el trabajo es de configuración, no de código nuevo: `Storage__Provider=gcs`, `GOOGLE_CLOUD_PROJECT=tivit-cu010`, `Storage__Bucket=tivit-cu010-mpm-adjuntos` como variables de entorno del servicio/Jobs de Cloud Run (ya no en un `docker-compose.prod.yml` de una VM).

### 3. Credenciales GCP sin exponer en el repo

```text
- Service Account de GCP con permisos mínimos (Storage Object Admin sobre el bucket, Cloud SQL Client, roles/run.invoker
  para que Cloud Scheduler dispare los Jobs)
- Credenciales inyectadas vía la identidad de servicio nativa de Cloud Run (Workload Identity ya no aplica en el
  sentido de Compute Engine — cada servicio/Job de Cloud Run se ejecuta con su propia Service Account asociada)
- Secretos de aplicación (JWT_SECRET, GEMINI_API_KEY, MP_TICKET, credenciales de BD) en Secret Manager, montados como
  variables de entorno del servicio/Job — no en un .env de una VM que ya no existe
```

### 4. Scripts operacionales

```text
scripts/
├── backup-db.sh                   ← pg_dump + gzip + upload a GCS, o snapshot automático si es Cloud SQL
├── restore-db.sh                  ← Descarga/snapshot + restore
└── deploy.sh                      ← REVISAR: hoy asume `docker compose` sobre una VM (`gcloud compute ssh`).
                                       Pasa a `gcloud run deploy` para el servicio web y `gcloud run jobs deploy` +
                                       `gcloud scheduler jobs create` / `gcloud pubsub` para los tres Jobs.
```

---

## Module Structure

No se crea un nuevo módulo .NET ni un nuevo `IStorageService`. El "modo worker" del punto 1 vive dentro de `MPM.Api` (mismo binario), no como proyectos nuevos — mantiene el Principio I. Cambios en:

```text
src/MPM.Api/Program.cs                          ← Modo worker para Cloud Run Jobs (nuevo)
Dockerfile(s) por target                        ← Nuevo: puede seguir siendo la misma imagen (el modo se decide por
                                                    env var al arrancar el contenedor), o una imagen liviana sin
                                                    Kestrel para los Jobs si el tamaño de imagen importa
scripts/backup-db.sh                            ← Nuevo
scripts/restore-db.sh                            ← Nuevo
scripts/deploy.sh                                ← Revisar (Cloud Run en vez de Compute Engine)
docs/runbook-produccion.md                       ← Nuevo
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | El "modo worker" (punto 1) es un entry point distinto del mismo binario `MPM.Api`, no un módulo ni proyecto nuevo. Los tres background services siguen viviendo en sus módulos (`MPM.Modules.Licitaciones`, `MPM.Modules.Analisis`) — solo cambia qué los invoca (Cloud Scheduler/Pub-Sub en vez de un `Timer` interno). |
| **II. Stored Procedures First** | ✅ N/A | No hay cambios de BD |
| **III. Migraciones SQL** | ✅ N/A | No hay nuevas migraciones |
| **IV. Multi-Tenancy** | ✅ Sin violación | JWT y TenantContext sin cambios |
| **V. Abstracción de Storage** | ✅ Ya cumplido | `GcsStorageService` ya implementa `IStorageService`; esta fase solo activa `Storage__Provider=gcs` en producción |
| **VI. Real-Time via SignalR + Redis Backplane** | ⚠️ Requiere validación | SignalR se mantiene en el servicio Cloud Run del API con `min-instances >= 1`; el backplane pasa de Redis-en-contenedor a Memorystore. No cambia el código (`AddStackExchangeRedis` sigue igual), solo el endpoint de conexión — validar en `research.md` que la latencia del Serverless VPC Connector no degrada la experiencia de chat. |

---

## Información requerida — resuelta 2026-07-03

| Dato | Respuesta del cliente | Estado real verificado en GCP |
|---|---|---|
| Región | `us-central1` | Coincide con la región del bucket y de `mpm-db` (`us-central1`) — sin costo de tráfico entre regiones |
| Base de datos | Cloud SQL (confirmado) | **Ya existe**: instancia `mpm-db`, Postgres 16, `us-central1-a`, tier `db-f1-micro`, base `mpm` creada, backups diarios activos |
| Dominio | Ninguno todavía — usar IP/URL genérica de la VM | Se documenta como pendiente para cuando exista; no bloquea el despliegue |
| Service Account | Proyecto separado `TIVIT CU010` (`tivit-cu010`) | Confirmado y accesible; ninguna SA existente (`agente-mercado-publico`, `Gemini API Key`, default de Compute) tiene los permisos correctos — se crea una dedicada |
| Credenciales de app | Rotar `JWT_SECRET`, mantener el resto | Se genera un secreto nuevo en el momento del deploy (nunca en el repo ni en el chat) |

⚠️ **Hallazgo no solicitado pero relevante**: `mpm-db` tiene hoy `authorizedNetworks=0.0.0.0/0` y SSL opcional — abierta a cualquier IP de internet. Se corrige como parte de esta fase usando Cloud SQL Auth Proxy (ver `research.md`) en vez de conexión directa por IP pública.

⚠️ **Actualización 2026-07-06 — restricciones de infraestructura TIVIT, cambian la arquitectura de esta fase**: Nicolás Valdivia (consultor cloud) respondió a la solicitud de recursos (`solicitud-consultor-cloud.md`) con 3 bloqueos que invalidan supuestos del plan original — ver detalle y decisiones revisadas en `research.md` secciones 5, 5b y "Actualización 2026-07-06":
1. **No usar la VPC default** — cada ambiente necesita su propia VPC/subnet custom (confirma y hace obligatoria la solicitud de segmentación, ver `solicitud-segmentacion-red.md`).
2. **La VM `mpm-prod` no puede tener IP pública** — la exposición pública debe ser vía GCP HTTPS Load Balancer, nunca por IP directa de la VM. Esto invalida el plan de TLS con certbot en la VM (sección 5 de `research.md`) y el acceso SSH directo (se reemplaza por IAP tunneling).
3. **Cloud SQL no puede tener IP pública en ningún caso** (más estricto que solo quitar `0.0.0.0/0`) — requiere Private Services Access con un rango de IP dedicado, peered a la VPC custom.

Impacto en artefactos ya generados: `docker-compose.prod.yml` y `quickstart.md` asumían IP pública en la VM y certbot — **quedan desactualizados y deben revisarse** antes de implementar. No se re-generó `research.md` desde cero, se marcaron las secciones afectadas como "REVISADA 2026-07-06" para no perder el razonamiento original.

---

## Artefactos

- [x] `research.md` — **reescrito 2026-07-06**: Cloud Run + Cloud Run Jobs (ver §1 y §1b), Cloud SQL Private IP vía Serverless VPC Access Connector, Memorystore para Redis, VPC custom (§5b), TLS nativo de Cloud Run (§5)
- [x] `data-model.md` — N/A, documentado explícitamente (fase de infraestructura, sin cambios de esquema)
- [ ] `quickstart.md` — **desactualizado, pendiente de reescribir**: los 7 escenarios asumen `docker-compose.prod.yml` sobre una VM con IP externa y certbot; hay que rehacerlos para `gcloud run services describe` / `gcloud run jobs execute` y la URL `*.run.app`
- [ ] `docker-compose.prod.yml` — **obsoleto para el servicio web**: Cloud Run no usa Docker Compose para desplegar; puede conservarse solo como referencia para levantar el stack localmente en modo "simula prod", pero no es el artefacto de despliegue real. Se reemplaza por comandos `gcloud run deploy` / `gcloud run jobs deploy` en `deploy.sh`.
- [ ] `scripts/deploy.sh` — **reescribir**: pasa de `docker compose` sobre `gcloud compute ssh` a `gcloud run deploy` (servicio web) + `gcloud run jobs deploy` (los 3 Jobs)
- [ ] `docs/runbook-produccion.md` — pendiente, se genera junto con `tasks.md`
- [x] `tasks.md` — **regenerado 2026-07-06** vía `/speckit-tasks`: 36 tareas en 6 fases (Setup infra, Foundational "modo worker", US1 acceso público, US2 GCS/Cloud SQL, US3 deploy repetible, Polish). T007 marca explícitamente el bloqueo por `016-extraccion-documentos-api`.
- [x] `solicitud-segmentacion-red.md` — respuesta a Nicolás lista para enviar: VPC custom con segmentos PRD/QA, sin IP pública (ahora aplica igual a Cloud Run vía Serverless VPC Access Connector en vez de a una VM), rango de Private Services Access para Cloud SQL
- [x] ~~Bloqueante: 016 debe implementarse antes de crear scraper-job~~ **Revertido 2026-07-06** — ver spec.md "Actualización 2026-07-06"
- [x] `solicitud-recursos-cloud-run.md` — **nuevo 2026-07-06**: inventario real de `tivit-cu010` (vía `gcloud`, solo lectura) y checklist exacto de lo que falta pedirle a Nicolás para poder desplegar (APIs, VPC/Connector, mover Cloud SQL de la VPC default, Memorystore, Service Accounts, Artifact Registry, Secret Manager). Pendiente de enviar.
