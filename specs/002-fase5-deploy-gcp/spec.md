# Feature Specification: Fase 5 — Despliegue en GCP

**Feature Branch**: `002-fase5-deploy-gcp`
**Created**: 2026-06-24
**Updated**: 2026-07-03 (retargeteado de On-Premise + Huawei Cloud a Google Cloud Platform, y elevado a prioridad N1)
**Status**: Planned
**Semana estimada**: N1 — inmediata (Julio 2026, antes que cualquier otro ítem del roadmap)
**Impacto**: Alto | **Complejidad**: Alta | **Depende de**: —

**Actualización 2026-07-03**: El cliente repriorizó el roadmap y ubicó el despliegue en producción como la prioridad N1 — "lo que haremos ahora", por delante incluso de Alertas. Esta versión reemplaza el enfoque anterior (servidor on-premise + Huawei OBS) por **Google Cloud Platform**, alineado con trabajo ya iniciado en el repositorio: el proyecto GCP `tivit-cu010` y el bucket `tivit-cu010-mpm-adjuntos` ya existen y se usaron en un commit reciente (`fix(env): centralize config in .env and migrate storage to GCS`), y `GcsStorageService` ya está implementado en `MPM.Shared`/`IStorageService`. Esta fase termina de dejar el sistema corriendo en GCP de forma estable, no empieza desde cero.

---

## Contexto

El sistema MPM corre hoy solo en Docker local en el equipo del equipo de desarrollo. El cliente necesita que el equipo comercial (Francisco y los account managers de gobierno) pueda acceder al sistema en producción antes de que el resto de funcionalidades (Alertas, Buscador, Pipeline) tengan sentido de usar en el día a día — de nada sirve tener alertas inteligentes si nadie puede entrar al sistema fuera del laptop de un desarrollador. Por eso se eleva a N1: es la base sobre la que corre todo lo demás.

---

## User Stories

### User Story 1 — Sistema accesible en producción (Priority: P1)

Francisco y el equipo comercial necesitan acceder al sistema desde cualquier lugar, en una URL estable, sin depender de que el laptop de un desarrollador esté encendido.

**Why this priority**: Sin esto, ninguna otra fase del roadmap (Alertas, Buscador, Pipeline) puede usarse operativamente — es el prerequisito de todo.

**Independent Test**: Cualquier miembro del equipo TIVIT accede a la URL pública del sistema desde su navegador, se loguea y ve el dashboard sin intervención técnica.

**Acceptance Scenarios**:
1. **Given** el servicio está desplegado en GCP, **When** el usuario accede a la URL HTTPS pública, **Then** el login funciona y el certificado es válido.
2. **Given** el sistema corre en producción, **When** se revisan los servicios (api, web, db, redis), **Then** todos reportan estado saludable.
3. **Given** el servicio se reinicia (deploy, actualización, o caída), **When** vuelve a levantar, **Then** lo hace sin intervención manual y sin pérdida de datos.

---

### User Story 2 — Archivos y base de datos gestionados en GCP (Priority: P1)

El sistema necesita almacenar los PDFs de actas/bases y la base de datos en servicios gestionados de GCP, no en disco local efímero, para no perder información ni depender de un único servidor.

**Why this priority**: Los análisis de Gemini representan horas de procesamiento y los documentos son la evidencia legal de cada licitación — perderlos sería crítico para el negocio.

**Independent Test**: Al subir un PDF de análisis en producción, el archivo queda en el bucket `tivit-cu010-mpm-adjuntos` de GCS, y un dump reciente de la base de datos existe y es restaurable.

**Acceptance Scenarios**:
1. **Given** `Storage__Provider=gcs` está configurado en producción, **When** se sube un documento, **Then** el archivo aparece en el bucket GCS y la URL almacenada en BD apunta a GCS, no a disco local.
2. **Given** la base de datos corre en un servicio gestionado (Cloud SQL) o con backup automático, **When** se ejecuta un restore de prueba, **Then** el sistema queda operativo en menos de 30 minutos.

---

### User Story 3 — Actualizaciones sin downtime perceptible (Priority: P2)

El equipo de desarrollo necesita poder desplegar nuevas versiones (nuevas fases del roadmap) sin que el equipo comercial pierda acceso al sistema por períodos largos.

**Why this priority**: A partir de esta fase se van a desplegar Alertas, Buscador y Pipeline de forma incremental — cada deploy no puede tumbar el sistema por horas.

**Independent Test**: Se ejecuta el proceso de deploy con un cambio de código y el sistema vuelve a estar disponible en minutos, no horas, sin pasos manuales adicionales a correr un script/pipeline.

**Acceptance Scenarios**:
1. **Given** un cambio de código listo para desplegar, **When** se ejecuta el proceso de deploy, **Then** la nueva versión queda corriendo sin intervención manual más allá de disparar el proceso.
2. **Given** un deploy en curso, **When** falla algún paso, **Then** el sistema anterior sigue respondiendo (no queda en un estado a medio actualizar).

---

### Edge Cases

- ¿Qué pasa si el bucket GCS o la base de datos gestionada no están disponibles momentáneamente? El sistema debe fallar de forma visible (error claro) en vez de silenciar o corromper datos.
- ¿Cómo se manejan las credenciales de GCP (service account) de forma segura, sin quedar en el repositorio ni en la imagen de Docker?
- ¿Qué pasa con los servicios en memoria (Redis, SignalR backplane) si la instancia se reinicia? Las conexiones activas deben poder reconectar sin que el usuario pierda su sesión de forma permanente.

## Requirements

### Functional Requirements

- **FR-001**: El sistema MUST estar accesible en una URL HTTPS pública y estable para el equipo de TIVIT.
- **FR-002**: El sistema MUST almacenar todo archivo subido (PDFs de actas/bases) en el bucket GCS `tivit-cu010-mpm-adjuntos` en producción, usando el `GcsStorageService` ya existente — no en disco local del contenedor.
- **FR-003**: El sistema MUST persistir la base de datos PostgreSQL en un servicio con backup recuperable (gestionado por GCP o con backup automático a GCS), no solo en un volumen local de un único servidor.
- **FR-004**: El sistema MUST reiniciar automáticamente sus servicios ante una caída o reinicio de la instancia, sin intervención manual.
- **FR-005**: El sistema MUST permitir desplegar una nueva versión de código sin exponer credenciales (JWT secret, API keys, credenciales de BD) en el repositorio.
- **FR-006**: El sistema MUST mantener un certificado TLS válido con renovación automática.
- **FR-007**: El proceso de despliegue MUST quedar documentado en un runbook que cualquier persona del equipo pueda seguir sin conocimiento previo del entorno.

## Success Criteria

### Measurable Outcomes

- **SC-001**: El equipo comercial puede acceder al sistema en producción desde cualquier ubicación, cualquier día, sin depender de un equipo de desarrollo encendido.
- **SC-002**: Cero pérdida de documentos o de datos de análisis ante un reinicio o falla de la instancia de cómputo.
- **SC-003**: Un despliegue de una nueva versión toma menos de 15 minutos y no requiere pasos manuales fuera de lo documentado en el runbook.
- **SC-004**: Un restore completo de base de datos desde backup toma menos de 30 minutos.

## Assumptions

- El proyecto GCP `tivit-cu010` y el bucket `tivit-cu010-mpm-adjuntos` ya existen (evidenciado en el historial de commits) y se reutilizan tal cual — no se crean desde cero.
- Dado que el sistema depende de background services de larga duración (`SyncEngineService`, `ScraperBackgroundService`, `AnalisisBackgroundService`) y de SignalR con backplane Redis, se asume que el cómputo corre sobre una instancia persistente (Compute Engine) más que sobre un modelo serverless de request/response (Cloud Run), salvo que `/speckit-plan` determine lo contrario tras evaluar costos y complejidad.
- La base de datos usa Cloud SQL para PostgreSQL como servicio gestionado, evitando administrar backups manuales de un Postgres self-hosted — a confirmar en `/speckit-plan` frente al costo de mantener el Postgres actual en contenedor con backup a GCS.
- Redis puede mantenerse como contenedor junto a la aplicación (no es dato crítico de negocio, es cache/backplane) en vez de usar Memorystore, salvo que la disponibilidad lo justifique.
- Fuera de alcance en esta fase: autoescalado horizontal, multi-región, y CI/CD completamente automatizado (el deploy puede ser un script ejecutado manualmente por el equipo, no un pipeline con gates).
