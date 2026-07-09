# Runbook de Producción — MPM en Cloud Run

**Spec**: `specs/002-fase5-deploy-gcp/` | **Actualizado**: 2026-07-06

Este runbook asume que la infraestructura de la Fase 1 de `specs/002-fase5-deploy-gcp/tasks.md`
(VPC custom, Serverless VPC Access Connector, Cloud SQL con Private IP, Memorystore, Service
Accounts) ya existe. No cubre cómo crearla — eso depende del equipo de infraestructura de TIVIT
(ver `specs/002-fase5-deploy-gcp/solicitud-segmentacion-red.md`).

## Checklist real de "plug and play" (actualizado 2026-07-06)

Auditamos `deploy.sh` y el Dockerfile del frontend contra lo que realmente hace falta para un
deploy real (no solo "compila"). Se encontraron y corrigieron 2 gaps que habrían roto el
primer deploy silenciosamente:

- ✅ **Corregido**: `nginx.conf` del frontend tenía hardcodeado `proxy_pass http://api:80`
  (nombre de Docker Compose, no existe en Cloud Run). Ahora es un template
  (`nginx.conf.template`) que resuelve `API_URL` y `PORT` en runtime vía `envsubst` nativo de
  la imagen `nginx:alpine`. Validado localmente con `docker build` + `docker run` simulando
  variables de Cloud Run — responde 200 con el puerto inyectado.
- ✅ **Corregido**: `deploy.sh` no pasaba ninguna variable de entorno de la aplicación al
  `gcloud run deploy` — el contenedor habría arrancado sin connection string de BD, sin Redis,
  sin storage. Ahora arma `--set-env-vars`/`--set-secrets` completos, y valida con
  `require_var` que las variables de infraestructura estén seteadas antes de intentar nada
  (falla con mensaje claro, no en silencio).
- ✅ **Corregido**: `cloudbuild.googleapis.com` no estaba habilitada (`deploy.sh` la necesita
  para `gcloud builds submit`) — habilitada.
- ✅ **Nuevo**: `scripts/setup-secrets.sh` arma los secretos de Secret Manager que
  `deploy.sh` espera (`jwt-secret`, `gemini-api-key`, `mp-ticket`,
  `postgresql-connection-string`) — no se corrió todavía, requiere valores reales (`JWT_SECRET`
  debe ser uno **nuevo**, no reusar el de dev).

### Lo que falta para el primer deploy real (en orden de dependencia)

1. **De Nicolás** (ver `solicitud-recursos-cloud-run.md`): VPC custom + Serverless VPC Access
   Connector, mover `mpm-db` a esa VPC, Memorystore, y los 3 roles de proyecto en
   `mpm-api-sa`/`mpm-jobs-sa` (bloqueados por permisos, confirmado empíricamente).
2. **Nuestro, una vez lo anterior exista**: correr `scripts/setup-secrets.sh` con los valores
   reales (`JWT_SECRET` nuevo, `GEMINI_API_KEY`, `MP_TICKET`, credenciales de BD, y la IP
   privada real de `mpm-db` una vez movida).
3. **Nuestro**: `scripts/deploy.sh prod api up`, luego `prod web up` (necesita que `api` ya
   esté desplegado para tomar su URL), luego `prod sync-job up` y `prod scraper-job up`.
4. **Nuestro, no bloqueante para el primer deploy**: crear los Cloud Scheduler jobs que
   disparan `sync-job`/`scraper-job` automáticamente (hoy solo se pueden ejecutar a mano con
   `scripts/deploy.sh prod scraper-job execute`) — no scripteado todavía (T028 de tasks.md).
5. **Riesgo aceptado, no resuelto**: `AnalisisBackgroundService` sigue siendo `Task.Run`
   fire-and-forget dentro de `mpm-api` — funciona, pero corre el riesgo de cortarse si Cloud
   Run le quita CPU justo después de que la request HTTP que lo disparó termine. Aceptable
   para un primer lanzamiento de bajo volumen; no resuelto (ver más abajo).

Con los puntos 1-3 resueltos, **sí es plug-and-play** — no hace falta tocar código de nuevo,
solo ejecutar los scripts en orden.

## Prerrequisitos antes del primer deploy

- [x] VPC custom `vpc-cu010` + subred `sn-cu010-prd` (10.0.0.0/24) creadas en `us-central1` (confirmado por Nicolás 2026-07-07, verificado vía `gcloud`)
- [x] `mpm-db` (Cloud SQL) migrada a Private IP en `vpc-cu010`, sin IP pública, `sslMode=ENCRYPTED_ONLY`
- [x] Instancia Memorystore para Redis (`redis-cu010`) en `vpc-cu010` — ⚠️ tier `BASIC` (sin failover), el correo de Nicolás decía "capacidad estándar"; confirmar si es intencional o si se necesita `STANDARD_HA`
- [x] Service Accounts `mpm-api-sa` y `mpm-jobs-sa` creadas — ⚠️ falta `roles/aiplatform.user` en ambas (verificado 2026-07-07 vía `gcloud projects get-iam-policy`), sin esto Gemini/Vertex AI responde 403 en prod
- [ ] Artifact Registry `mpm` creado en `us-central1` para las imágenes
- [ ] Secretos cargados en Secret Manager: `JWT_SECRET` (rotado), `GEMINI_API_KEY`, `MP_TICKET`, credenciales de BD
- [ ] **`016-extraccion-documentos-api` implementada** — ya no es bloqueante (ver `ROADMAP.md`, pivote a Cloud Run Jobs que no throttlean CPU)
- **Nota 2026-07-07**: no se usa Serverless VPC Access Connector — Cloud Run se conecta a la VPC vía Direct VPC egress (`--network`/`--subnet`), confirmado por Nicolás que no hace falta Connector con Cloud SQL/Memorystore en la misma VPC.

## Deploy del servicio web

```bash
scripts/deploy.sh prod api up
```

Internamente: build de la imagen (`gcloud builds submit`), push a Artifact Registry, y
`gcloud run deploy mpm-api` con `--min-instances=1` (necesario para que SignalR no pierda
sesiones activas de chat por cold-start), `--no-cpu-throttling` (mitigación de piso para que el
análisis IA no muera cuando Cloud Run retira la CPU entre peticiones — ver
`specs/022-qa-fixes-preproduccion/`, BUG-002) y `--network`/`--subnet` (Direct VPC egress)
apuntando a `vpc-cu010`/`sn-cu010-prd` para llegar a Cloud SQL/Memorystore por IP privada.

El servicio web arranca con `RUN_INPROCESS_WORKERS=false`: `SyncEngineService`,
`ScraperBackgroundService` y `AclaracionMonitorService` NO corren dentro de este proceso (ya
corren como los Cloud Run Jobs de abajo) — evita la sincronización duplicada de BUG-004. En
Docker Compose local esta variable no se setea y el default (`true`) preserva el comportamiento
actual. `Cors:AllowedOrigins` también debe apuntar al dominio real de `mpm-web` una vez
desplegado (ver `CORS_ALLOWED_ORIGINS` en `scripts/deploy.sh`) — sin esto, el frontend de
producción queda bloqueado por CORS.

## Deploy de los background services (Cloud Run Jobs)

```bash
scripts/deploy.sh prod sync-job up
scripts/deploy.sh prod scraper-job up
```

Cada uno despliega la misma imagen del servicio web, pero con `WORKER_MODE=sync` o
`WORKER_MODE=scraper` — `src/MPM.Api/Program.cs` detecta esa variable y ejecuta un solo ciclo
del background service correspondiente en vez de levantar Kestrel (ver
`SyncEngineService.EjecutarCicloUnaVezAsync()` / `ScraperBackgroundService.EjecutarCicloUnaVezAsync()`).

**`analisis-job` no existe todavía.** `AnalisisBackgroundService` no es un ciclo periódico —
sigue disparando `Task.Run` fire-and-forget dentro del proceso web cuando se sube un documento
(`EnqueueAnalisis`). Rediseñarlo como Job requiere un mecanismo de cola real (Pub/Sub) en vez de
`Task.Run` — no es solo exponer un "ejecutar una vez" como se hizo con Sync/Scraper. **Pendiente,
no implementado en esta pasada.**

**Mitigación agregada (2026-07-08, `specs/022-qa-fixes-preproduccion`, BUG-002)**: como piso de
seguridad mientras no existe el rediseño completo, se agregó `AnalisisRecoveryWorker` — un
`IHostedService` que corre cada ~60s dentro del propio servicio web y reencola cualquier
análisis que quedó en estado `analizando` sin resultado por más de `Analisis:RecoveryThresholdMinutes`
(default 5 min), reutilizando el mismo `estado` que ya se persistía. Sumado a
`--no-cpu-throttling` (arriba), esto reduce drásticamente el caso de "queda procesando para
siempre", sin requerir el rediseño a Pub/Sub todavía.

## Estado de `016-extraccion-documentos-api` — spike ejecutado, resultado: bloqueado por reCAPTCHA

Implementada a nivel de código (`WebFormsParser`, `MpSessionProvider`, `AdjuntosHttpExtractor`,
`DocumentExtractionService`, migración `V078`). El spike de descubrimiento (T004) se ejecutó
el 2026-07-06 con credenciales reales contra el portal, y encontró que el listado de adjuntos
(`ViewAttachment.aspx`) está protegido por **Google reCAPTCHA Enterprise ejecutado client-side**
— un `HttpClient` sin motor de JavaScript no puede resolverlo; solo un navegador real lo pasa
de forma transparente. Detalle completo en
`specs/016-extraccion-documentos-api/contracts/internal-api.md`.

**Consecuencia**: `Extraccion:Modo` se mantiene en `solo_navegador` (default) indefinidamente,
salvo que se encuentre una forma de resolver el challenge (por ejemplo, un paso híbrido con
navegador solo para ese hop puntual — no implementado). `AdjuntosHttpExtractor` sigue siendo
código correcto y reutilizable para la parte que sí es HTTP puro (resolución de ficha por
código + extracción del token `enc`), pero falla de forma controlada y documentada en el
paso del reCAPTCHA y cae al fallback de navegador.

**Esto NO bloquea `scraper-job`**: Cloud Run Jobs no throttlean CPU por inactividad de
requests (a diferencia de Cloud Run Services) — corren hasta completarse con recursos
completos, como un proceso batch. `scraper-job` puede correr el ciclo completo con Chromium,
de la misma duración que hoy, sin problema. Solo se pierde el beneficio adicional de
"ejecución corta" que se esperaba de 016.

## Logs y monitoreo

```bash
scripts/deploy.sh prod api logs
scripts/deploy.sh prod sync-job logs
scripts/deploy.sh prod scraper-job logs
scripts/deploy.sh prod all status
```

## Rollback

Cloud Run mantiene revisiones anteriores. Si una nueva revisión del servicio web falla el
health check, Cloud Run no le enruta tráfico automáticamente — no se requiere rollback manual
en el caso común. Si hace falta forzar una revisión anterior:

```bash
gcloud run services update-traffic mpm-api --region us-central1 --to-revisions=<revision-anterior>=100
```

## Backup y restore de base de datos

```bash
scripts/backup-db.sh                                          # backup manual a GCS
scripts/restore-db.sh gs://.../backup.sql.gz --target <instancia-de-prueba>   # restore (nunca directo a mpm-db sin confirmar)
```

## Migraciones de base de datos

Las migraciones (`src/MPM.Api/Database/Scripts/VXXX__*.sql`) se aplican automáticamente al
iniciar el servicio web (`DatabaseInitializer`). **Los Cloud Run Jobs (`WORKER_MODE` activo)
NO corren migraciones** — se asume que el servicio web ya las aplicó. Si se despliega un Job
antes que el servicio web haya corrido al menos una vez con el esquema nuevo, el Job puede
fallar por falta de una tabla/columna/SP recién agregada.
