# Solicitud a Nicolás — Recursos faltantes para desplegar MPM en Cloud Run

**Contexto**: Inventario de `tivit-cu010` hecho el 2026-07-06 vía `gcloud` (cuenta
`matias.mendez@tivit.com`, con autorización explícita del usuario para ejecutar comandos).
Se hizo todo lo que los permisos de esa cuenta permitieron; lo que sigue abajo es
exactamente lo que falta y requiere a Nicolás (Owner del proyecto).

## ✅ Ya hecho (2026-07-06, verificado)

- **APIs habilitadas**: `run`, `artifactregistry`, `vpcaccess`, `redis`, `secretmanager`, `pubsub` (antes solo estaban `compute`, `iam`, `servicenetworking`, `sqladmin`, `cloudscheduler`).
- **Artifact Registry**: repo Docker `mpm` creado en `us-central1`.
- **Service Accounts**: `mpm-api-sa@tivit-cu010.iam.gserviceaccount.com` y `mpm-jobs-sa@tivit-cu010.iam.gserviceaccount.com` creadas.
- **IAM a nivel de bucket**: ambas SA tienen `roles/storage.objectAdmin` sobre `gs://tivit-cu010-mpm-adjuntos` (scopeado al bucket, no rol de proyecto).
- **Ya existía**: `mpm-db` (Cloud SQL Postgres 16) con `ipv4Enabled=false` — sin IP pública.

## ✅ Resuelto por Nicolás (2026-07-07, verificado vía `gcloud`)

- **Red**: VPC custom `vpc-cu010` + subred `sn-cu010-prd` (10.0.0.0/24) en `us-central1`. QA queda pendiente a propósito (Nicolás propone un proyecto GCP separado para QA, no una subnet en el mismo proyecto — de acuerdo, se pedirá cuando haga falta).
- **Conectividad**: sin Serverless VPC Access Connector — Cloud Run se conecta a `vpc-cu010` vía Direct VPC egress (`--network`/`--subnet`), confirmado que no hace falta Connector con Cloud SQL/Memorystore en la misma VPC. `scripts/deploy.sh` actualizado para usar `--network=vpc-cu010 --subnet=sn-cu010-prd` en vez de `--vpc-connector`.
- **Cloud SQL**: `mpm-db` ya estaba en Private IP dentro de `vpc-cu010`, sin IP pública ni `0.0.0.0/0`, `sslMode=ENCRYPTED_ONLY`.
- **Memorystore**: instancia `redis-cu010` (`REDIS_7_2`) creada en `vpc-cu010`.
- **IAM**: `roles/cloudsql.client` y `roles/secretmanager.secretAccessor` otorgados a `mpm-api-sa` y `mpm-jobs-sa`; `roles/run.invoker` otorgado a `mpm-jobs-sa`.

## ✅ Cerrado 2026-07-07: `roles/aiplatform.user` otorgado

Nicolás asignó `roles/aiplatform.user` a `mpm-api-sa` y `mpm-jobs-sa` — verificado vía
`gcloud projects get-iam-policy`, ambas SA lo tienen junto con el resto de los roles.
**No quedan pendientes de IAM.** Ya se puede intentar el primer `scripts/deploy.sh prod api up`.

## ✅ Memorystore — tier `BASIC`, no requiere cambio

El correo de Nicolás decía "capacidad estándar" pero la instancia `redis-cu010` quedó en tier `BASIC` (sin failover/réplica). Evaluado y **`BASIC` es suficiente para este caso**: Redis en MPM se usa únicamente como backplane de SignalR (mensajería en tiempo real), sin datos persistentes ni de negocio — si la instancia se reinicia por mantenimiento (el único escenario de downtime real en `BASIC`), los clientes de chat simplemente reconectan solos, sin pérdida de datos. `STANDARD_HA` (failover automático, ~2x el costo) solo se justifica cuando Redis es fuente de verdad de algo que no se puede regenerar, que no es este caso. No hace falta pedirle a Nicolás que lo cambie — además el tier no se puede modificar in-place en Memorystore, requeriría recrear la instancia.

## Resumen

Con el rol de IAM faltante (punto 1) y la confirmación del tier de Redis (punto 2), MPM queda
listo para el primer `scripts/deploy.sh prod api up`. Todo lo demás (imágenes, Cloud Run
service/jobs, Secret Manager con los secretos de la app) lo hacemos nosotros con lo que ya
está habilitado.
