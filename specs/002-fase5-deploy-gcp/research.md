# Research: Fase 5 — Despliegue en GCP

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Fecha**: 2026-07-03

Resuelve las decisiones técnicas marcadas como abiertas en `plan.md`. Cada decisión está fundamentada en evidencia real del repositorio (Dockerfiles, `docker-compose.yml`, background services existentes), no en supuestos genéricos de "mejores prácticas de GCP".

---

## 1. Cómputo: Compute Engine vs. Cloud Run

**Decision**: Compute Engine (VM persistente corriendo el mismo `docker-compose.yml` de hoy, adaptado a producción).

**Rationale**:
- `src/MPM.Api/Dockerfile` instala **Node.js 20 + Playwright Chromium dentro del propio contenedor de la API**, porque `ScraperBackgroundService` ejecuta `tools/scraper-mp/agente-mp.js` con un navegador headless real. Un navegador Chromium completo corriendo procesos de scraping de larga duración no encaja bien en el modelo de Cloud Run: instancias que se reciclan por inactividad o se escalan a cero cortarían jobs de scraping a mitad de ejecución.
- Además de `ScraperBackgroundService`, el mismo proceso corre `SyncEngineService` (sync diario) y `AnalisisBackgroundService` (cola de análisis Gemini) — tres background services de larga duración en un solo proceso .NET. Cloud Run está optimizado para request/response, no para procesos de fondo persistentes dentro de la misma instancia.
- SignalR con backplane Redis (`/hubs/mensajeria`) mantiene conexiones WebSocket abiertas por horas — Cloud Run soporta WebSockets pero con límites de duración de instancia que complican conexiones de chat de larga duración sin trabajo adicional de reconexión.
- Compute Engine reutiliza el `docker-compose.yml` casi sin cambios (ya probado en desarrollo), lo que reduce el riesgo del primer despliegue en producción — prioridad explícita del cliente al marcar esto N1 "para ahora".

**Alternatives considered**:
- **Cloud Run**: descartado para esta fase por las razones anteriores. Queda como posible evolución futura si el scraper y los background services se separan en servicios independientes (fuera de alcance de esta fase — ver Assumptions en `spec.md`).
- **GKE (Kubernetes)**: descartado por complejidad operativa desproporcionada para un sistema de un solo tenant interno con un equipo de desarrollo pequeño. Reevaluar solo si aparece necesidad real de autoescalado horizontal (explícitamente fuera de alcance en `spec.md`).

---

## 2. Base de datos: Cloud SQL vs. PostgreSQL en contenedor

**Decision**: Cloud SQL para PostgreSQL, como servicio gestionado.

**Rationale**:
- `docker-compose.yml` (historial de commits) ya tenía comentado un bloque `db` con la nota *"Descomentar para usar PostgreSQL local en lugar de Cloud SQL"* — es decir, Cloud SQL ya era la intención original del equipo antes de que el entorno de desarrollo local revirtiera temporalmente a Postgres en contenedor por conveniencia.
- FR-003 del spec exige que la base de datos sea recuperable ante falla de la instancia de cómputo. Un Postgres en contenedor sobre la misma VM comparte el mismo punto único de falla que el cómputo — Cloud SQL separa ese riesgo y provee backups automáticos y point-in-time recovery sin scripts propios de `pg_dump`/cron.
- El acceso ya es 100% vía stored procedures + Dapper (Principio II de la constitución) — Cloud SQL es un Postgres estándar, no requiere cambios de código, solo de connection string.

**Alternatives considered**:
- **PostgreSQL en contenedor en la misma VM + backup a GCS**: más barato mensualmente, pero reintroduce el riesgo que FR-003 busca evitar (single point of failure) y obliga a mantener scripts de backup/restore propios. Se documenta como alternativa de menor costo si el presupuesto aprobado no cubre Cloud SQL — **pendiente de confirmación del cliente** (ver "Qué necesito de tu parte").

---

## 3. Redis: contenedor vs. Memorystore

**Decision**: Redis como contenedor en la misma VM (sin Memorystore).

**Rationale**: Redis en este sistema es cache y backplane de SignalR — no almacena datos de negocio críticos que requieran durabilidad o HA gestionada. El costo adicional de Memorystore no se justifica en esta fase. Si la disponibilidad de mensajería en tiempo real se vuelve crítica más adelante, es un cambio de configuración simple (`ConnectionStrings__Redis`), no de código.

**Alternatives considered**: Memorystore for Redis — descartado por costo/beneficio en esta fase inicial.

---

## 4. Storage de archivos

**Decision**: GCS, bucket `tivit-cu010-mpm-adjuntos` (ya existente) vía `GcsStorageService` (ya implementado).

**Rationale**: No es una decisión nueva — ya está resuelta en el código y en la infraestructura GCP existente (`GOOGLE_CLOUD_PROJECT=tivit-cu010` visible en `docker-compose.yml`). Esta fase solo activa `Storage__Provider=gcs` en el compose de producción.

---

## 5. TLS y exposición pública

**Decision**: IP externa estática en la VM + DNS apuntando a esa IP + terminación TLS con Let's Encrypt (certbot) delante del contenedor `web` (nginx), que ya centraliza el proxy a `/api` y `/hubs` (ver `src/mpm-web/nginx.conf`).

**Rationale**: El contenedor `web` ya actúa como edge (sirve el frontend y proxea `/api`, `/hubs`, `/swagger`, `/health` al contenedor `api`). Agregar TLS ahí es el cambio de menor superficie para el primer despliegue. Un GCP HTTPS Load Balancer con certificado gestionado por Google es una alternativa más "cloud-native" pero agrega un componente más (backend service, health checks, NEG) sin beneficio claro todavía, dado que no hay requisito de autoescalado ni multi-región en esta fase.

**Alternatives considered**: GCP HTTPS Load Balancer + certificado gestionado — se deja como evolución natural si en el futuro se agrega autoescalado o un segundo backend.

---

## 6. Credenciales y secretos

**Decision**: Service Account de GCP con permisos mínimos (Storage Object Admin sobre `tivit-cu010-mpm-adjuntos`, Cloud SQL Client) montada en la VM vía Workload Identity/metadata de la instancia — no como archivo JSON copiado a la imagen. Secretos de aplicación (`JWT_SECRET`, `GEMINI_API_KEY`, `MP_TICKET`, credenciales de BD) en un `.env` de producción fuera del repositorio, con permisos de archivo restringidos en la VM (evaluar Secret Manager como mejora incremental, no bloqueante para el primer deploy).

**Rationale**: Cumple FR-005 (no exponer credenciales en el repositorio) con la menor complejidad operativa posible para una primera puesta en producción bajo presión de tiempo.

---

## Actualización 2026-07-03 — Estado real verificado en la consola de GCP

Inspección de solo lectura sobre el proyecto `tivit-cu010` (autorizada por el cliente):

- **Cloud SQL ya existe**: instancia `mpm-db`, PostgreSQL 16, `us-central1-a`, tier `db-f1-micro`, 10GB, con la base `mpm` ya creada y backups automáticos diarios (7 retenidos, 03:00). **No hay que crear Cloud SQL — solo conectar la aplicación a la instancia existente.**
- ⚠️ **Hallazgo de seguridad**: `mpm-db` tiene `authorizedNetworks` en `0.0.0.0/0` (abierta a cualquier IP de internet) y `sslMode: ALLOW_UNENCRYPTED_AND_ENCRYPTED` (SSL opcional, no exigido). Esto es un riesgo real hoy, independiente de esta fase. Se corrige como parte del despliegue (ver decisión de conexión abajo), no se deja así.
- **Compute Engine**: 0 instancias — la VM no existe, hay que crearla.
- **Bucket** `tivit-cu010-mpm-adjuntos`: existe, sin bindings IAM a nivel de bucket para ninguna service account específica todavía (solo roles legacy de Editor/Owner/Viewer del proyecto).
- **Service accounts existentes**: `agente-mercado-publico` y `Gemini API Key` (ninguna tiene roles de proyecto vinculados hoy — probablemente se usan solo para llamar la API de Gemini) y la SA default de Compute Engine. Ninguna es apta para usar tal cual — se crea una SA dedicada con permisos mínimos.
- **Firewall**: solo reglas default (SSH, RDP, interno, ICMP). No hay regla para tráfico web (80/443/8181) — hay que crearla.
- **Billing**: habilitado y activo en el proyecto.

**Decisión de conexión a Cloud SQL revisada**: en vez de conectar la API directamente a la IP pública de `mpm-db` (que hoy está abierta a `0.0.0.0/0`), se usa **Cloud SQL Auth Proxy** como contenedor sidecar en el mismo `docker-compose.prod.yml`, autenticado con la service account de la VM vía IAM (`roles/cloudsql.client`). Esto evita exponer la base de datos a internet y no requiere abrir la IP pública ni gestionar certificados SSL manualmente para Postgres. La corrección de `authorizedNetworks`/SSL de la instancia existente queda como tarea explícita del despliegue, no como algo a ignorar.

## Resumen de decisiones

| Decisión | Elegido | Pendiente de tu confirmación |
|---|---|---|
| Cómputo | Compute Engine (VM + Docker Compose) | Región/zona |
| Base de datos | **Cloud SQL para PostgreSQL** — confirmado por el cliente 2026-07-03 | — |
| Redis | Contenedor en la misma VM | — |
| Storage | GCS (`tivit-cu010-mpm-adjuntos`, ya existe) | — |
| TLS | Certbot delante de nginx en la VM | Dominio a usar |
| Secretos | Service Account + `.env` fuera del repo | Confirmar si ya existe Service Account o se crea uno nuevo |
