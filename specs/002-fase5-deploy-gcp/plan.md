# Implementation Plan: Fase 5 — Despliegue en GCP

**Branch**: `002-fase5-deploy-gcp` | **Status**: PLANIFICADO — listo para `/speckit-tasks`
**Spec**: [spec.md](./spec.md) | **Semana**: N1 — inmediata (Julio 2026)
**Actualizado**: 2026-07-03 — retargeteado de on-premise/Huawei OBS a GCP, elevado a prioridad N1; Fase 0/1 completadas (`research.md`, `data-model.md`, `quickstart.md`)

> `research.md`, `data-model.md` y `quickstart.md` ya están generados. Falta `/speckit-tasks` y las confirmaciones listadas en "Información requerida antes de implementar" antes de poder implementar.

---

## Summary

Llevar el sistema MPM de Docker local a producción en GCP. A diferencia del enfoque anterior (on-premise + Huawei OBS), esta fase **reutiliza infraestructura GCP ya provisionada**: proyecto `tivit-cu010` y bucket GCS `tivit-cu010-mpm-adjuntos` (visibles en `docker-compose.yml` y el commit `62c5bf2 fix(env): centralize config in .env and migrate storage to GCS`). `GcsStorageService` ya implementa `IStorageService` — no hace falta escribir un adaptador de storage nuevo, solo consolidar la configuración de producción alrededor de él.

---

## Technical Context

**Plataforma**: Google Cloud Platform, proyecto `tivit-cu010`
**Cómputo**: Compute Engine (instancia persistente corriendo Docker Compose) — preferido sobre Cloud Run por los background services de larga duración (`SyncEngineService`, `ScraperBackgroundService`, `AnalisisBackgroundService`) y el backplane de SignalR; confirmar en `research.md` si Cloud Run con instancias mínimas activas es viable para reducir costo
**Base de datos**: Cloud SQL para PostgreSQL (evaluar vs. Postgres en contenedor + backup a GCS, en `research.md`)
**Storage**: GCS, bucket `tivit-cu010-mpm-adjuntos` ya existente — reutiliza `GcsStorageService`
**Redis**: Contenedor junto a la app (no Memorystore, salvo que la disponibilidad lo requiera)
**TLS**: Certificado gestionado de GCP (Load Balancer) o Let's Encrypt si se opta por IP directa en la VM
**Gemini API**: Llamadas directas desde la instancia a `generativelanguage.googleapis.com` (sin cambios)
**Estimación**: 1 semana | **Complejidad**: Alta (primer despliegue en producción, aunque gran parte de la infraestructura GCP ya existe)

---

## Cambios de código requeridos

### 1. Configuración de producción para GCS (sin adaptador nuevo)

`GcsStorageService` ya existe — el trabajo es de configuración, no de código nuevo:

```text
docker-compose.prod.yml            ← Nuevo: Storage__Provider=gcs, GOOGLE_CLOUD_PROJECT=tivit-cu010,
                                       Storage__Bucket=tivit-cu010-mpm-adjuntos, restart policies
```

### 2. Credenciales GCP sin exponer en el repo

```text
- Service Account de GCP con permisos mínimos (Storage Object Admin sobre el bucket, Cloud SQL Client si aplica)
- Credenciales inyectadas vía Workload Identity (si Compute Engine) o Secret Manager, nunca commiteadas
```

### 3. Scripts operacionales

```text
scripts/
├── backup-db.sh                   ← pg_dump + gzip + upload a GCS, o snapshot automático si es Cloud SQL
├── restore-db.sh                  ← Descarga/snapshot + restore
└── deploy.sh                      ← git pull + docker compose up --build -d (o gcloud compute ssh + mismo comando)
```

---

## Module Structure

No se crea un nuevo módulo .NET ni un nuevo `IStorageService`. Cambios en:

```text
docker-compose.prod.yml                         ← Nuevo
scripts/backup-db.sh                            ← Nuevo
scripts/restore-db.sh                            ← Nuevo
scripts/deploy.sh                                ← Nuevo
docs/runbook-produccion.md                       ← Nuevo
```

---

## Constitution Check

| Principio | Estado | Justificación |
|---|---|---|
| **I. Modular Monolith** | ✅ Sin violación | No se modifica arquitectura de módulos |
| **II. Stored Procedures First** | ✅ N/A | No hay cambios de BD |
| **III. Migraciones SQL** | ✅ N/A | No hay nuevas migraciones |
| **IV. Multi-Tenancy** | ✅ Sin violación | JWT y TenantContext sin cambios |
| **V. Abstracción de Storage** | ✅ Ya cumplido | `GcsStorageService` ya implementa `IStorageService`; esta fase solo activa `Storage__Provider=gcs` en producción |

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

---

## Artefactos

- [x] `research.md` — decidido: Compute Engine (por Playwright/Chromium + background services + SignalR), Cloud SQL para Postgres (ya existe), Redis en contenedor, GCS ya resuelto, Cloud SQL Auth Proxy para la conexión a BD, TLS diferido hasta tener dominio
- [x] `data-model.md` — N/A, documentado explícitamente (fase de infraestructura, sin cambios de esquema)
- [x] `quickstart.md` — 7 escenarios de validación mapeados a SC-001..SC-004 y FR-001..FR-007
- [x] `docker-compose.prod.yml` — creado, con `cloudsql-proxy` sidecar y `Storage__Provider=gcs`
- [x] `scripts/deploy.sh` — creado: `deploy.sh <dev|prod> <all|api|web|...> [up|down|restart|logs|status|build]`
- [ ] `docs/runbook-produccion.md` — pendiente, se genera junto con `tasks.md`
- [ ] `tasks.md` — generado con `/speckit-tasks`, ya no bloqueado — toda la información requerida está resuelta
