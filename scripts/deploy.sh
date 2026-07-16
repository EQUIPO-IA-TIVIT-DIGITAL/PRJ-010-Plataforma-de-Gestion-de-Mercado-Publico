#!/usr/bin/env bash
# Deploy/gestión de MPM — local (dev, Docker Compose) o producción en GCP (prod, Cloud Run).
#
# Uso:
#   scripts/deploy.sh <env> <scope> [comando]
#
#   env:     dev | prod
#   scope (dev):  all | api | web | redis
#   scope (prod): all | api | web | sync-job | scraper-job | analisis-job
#   comando: up (default) | down | restart | logs | status | build
#
# Ejemplos:
#   scripts/deploy.sh dev all                    # levanta todo el stack local (sin cambios)
#   scripts/deploy.sh prod api                   # build + push + gcloud run deploy del backend
#   scripts/deploy.sh prod web                   # build + push + gcloud run deploy del frontend (apunta al backend ya desplegado)
#   scripts/deploy.sh prod sync-job up           # build + push + gcloud run jobs deploy sync-job
#   scripts/deploy.sh prod scraper-job execute   # ejecuta scraper-job ahora (fuera de su Scheduler)
#   scripts/deploy.sh prod all up                # api + web + sync-job + scraper-job, en ese orden
#
# ⚠️ REESCRITO 2026-07-06 para el pivote de Compute Engine a Cloud Run. Requiere que exista:
#   - VPC custom + subred (Nicolás confirmó 2026-07-07: vpc-cu010 / sn-cu010-prd, 10.0.0.0/24,
#     us-central1 — verificado con `gcloud compute networks/subnets list`)
#   - Cloud Run se conecta a esa VPC vía Direct VPC egress (--network/--subnet), NO vía
#     Serverless VPC Access Connector — Nicolás confirmó que no hace falta Connector al tener
#     Cloud SQL/Memorystore en la misma VPC con comunicación interna de Google habilitada
#   - Cloud SQL (mpm-db) con Private IP en esa VPC, e IP privada conocida (CLOUDSQL_PRIVATE_IP)
#   - Memorystore for Redis en esa VPC, host/puerto conocidos (REDIS_HOST/REDIS_PORT)
#   - Roles de proyecto en mpm-api-sa/mpm-jobs-sa (cloudsql.client, secretmanager.secretAccessor,
#     run.invoker, aiplatform.user) — verificado 2026-07-07 vía `gcloud projects get-iam-policy`:
#     falta `roles/aiplatform.user` en ambas SA (sin él, Gemini/Vertex AI responde 403 en prod),
#     ver specs/002-fase5-deploy-gcp/solicitud-recursos-cloud-run.md
#   - Secretos en Secret Manager (ver scripts/setup-secrets.sh) — JWT_SECRET, GEMINI_API_KEY,
#     MP_TICKET, DB_USER, DB_PASSWORD, REDIS_PASSWORD (si aplica)
# Sin esas cosas, este script fallará de forma clara (no silenciosa) al faltar variables.
#
# Variables de entorno para prod:
#   GCP_PROJECT          (default: tivit-cu010)
#   GCP_REGION            (default: us-central1)
#   RUN_API_SERVICE       (default: mpm-api)
#   RUN_WEB_SERVICE       (default: mpm-web)
#   RUN_SERVICE_SA        (default: mpm-api-sa@${GCP_PROJECT}.iam.gserviceaccount.com)
#   RUN_JOBS_SA           (default: mpm-jobs-sa@${GCP_PROJECT}.iam.gserviceaccount.com)
#   VPC_NETWORK           (default: vpc-cu010 — confirmado con Nicolás 2026-07-07)
#   VPC_SUBNET            (default: sn-cu010-prd — confirmado con Nicolás 2026-07-07)
#   ARTIFACT_REPO         (default: mpm)
#   IMAGE_TAG             (default: git rev-parse --short HEAD)
#   CLOUDSQL_PRIVATE_IP   REQUERIDO para api/jobs — IP privada de mpm-db una vez movida a la VPC custom
#   DB_NAME               (default: mpm)
#   REDIS_HOST            REQUERIDO para api — host de Memorystore
#   REDIS_PORT            (default: 6379)
#   GCS_BUCKET            (default: tivit-cu010-mpm-adjuntos)
#   SECRET_JWT / SECRET_GEMINI / SECRET_MP_TICKET / SECRET_DB_USER / SECRET_DB_PASSWORD
#                         (defaults: jwt-secret / gemini-api-key / mp-ticket / db-user / db-password
#                          — nombres de secretos en Secret Manager, ver scripts/setup-secrets.sh)

set -euo pipefail

ENV="${1:-}"
SCOPE="${2:-all}"
CMD="${3:-up}"

GCP_PROJECT="${GCP_PROJECT:-tivit-cu010}"
GCP_REGION="${GCP_REGION:-us-central1}"
RUN_API_SERVICE="${RUN_API_SERVICE:-mpm-api}"
RUN_WEB_SERVICE="${RUN_WEB_SERVICE:-mpm-web}"
RUN_SERVICE_SA="${RUN_SERVICE_SA:-mpm-api-sa@${GCP_PROJECT}.iam.gserviceaccount.com}"
RUN_JOBS_SA="${RUN_JOBS_SA:-mpm-jobs-sa@${GCP_PROJECT}.iam.gserviceaccount.com}"
VPC_NETWORK="${VPC_NETWORK:-vpc-cu010}"
VPC_SUBNET="${VPC_SUBNET:-sn-cu010-prd}"
ARTIFACT_REPO="${ARTIFACT_REPO:-mpm}"
IMAGE_TAG="${IMAGE_TAG:-$(git rev-parse --short HEAD 2>/dev/null || echo latest)}"
API_IMAGE="${GCP_REGION}-docker.pkg.dev/${GCP_PROJECT}/${ARTIFACT_REPO}/mpm-api:${IMAGE_TAG}"
WEB_IMAGE="${GCP_REGION}-docker.pkg.dev/${GCP_PROJECT}/${ARTIFACT_REPO}/mpm-web:${IMAGE_TAG}"

DB_NAME="${DB_NAME:-mpm}"
REDIS_PORT="${REDIS_PORT:-6379}"
GCS_BUCKET="${GCS_BUCKET:-tivit-cu010-mpm-adjuntos}"

SECRET_JWT="${SECRET_JWT:-jwt-secret}"
SECRET_MP_TICKET="${SECRET_MP_TICKET:-mp-ticket}"
SECRET_DB_CONNSTRING="${SECRET_DB_CONNSTRING:-postgresql-connection-string}"
SECRET_TELEGRAM_BOT_TOKEN="${SECRET_TELEGRAM_BOT_TOKEN:-telegram-bot-token}"
SECRET_TELEGRAM_WEBHOOK_SECRET="${SECRET_TELEGRAM_WEBHOOK_SECRET:-telegram-webhook-secret}"
TELEGRAM_BOT_USERNAME="${TELEGRAM_BOT_USERNAME:-CU010_bot}"
# Gemini ya NO usa API key (020-migracion-gemini-adc) — se autentica vía ADC con la identidad
# de mpm-api-sa/mpm-jobs-sa (roles/aiplatform.user), automático en Cloud Run, sin secreto.

usage() {
  echo "Uso: scripts/deploy.sh <dev|prod> <scope> [comando]"
  echo "  dev:  all | api | web | redis                              [up|down|restart|logs|status|build]"
  echo "  prod: all | api | web | sync-job | scraper-job | analisis-job   [up|execute|logs|status]"
  exit 1
}

[ -z "$ENV" ] && usage

# ── dev: sin cambios respecto al flujo anterior (Docker Compose local) ──────────────────
service_flag() {
  if [ "$SCOPE" = "all" ]; then echo ""; else echo "$SCOPE"; fi
}

run_dev() {
  local svc
  svc=$(service_flag)
  cd "$(dirname "$0")/.."
  case "$CMD" in
    up)      docker compose up --build -d $svc ;;
    down)    docker compose down $svc ;;
    restart) docker compose restart $svc ;;
    logs)    docker compose logs -f $svc ;;
    status)  docker compose ps ;;
    build)   docker compose build $svc ;;
    *) echo "Comando desconocido: $CMD"; usage ;;
  esac
}

# ── prod: Cloud Run (api + web) + Cloud Run Jobs (sync-job, scraper-job) ────────────────
require_var() {
  local name="$1"
  if [ -z "${!name:-}" ]; then
    echo "❌ Falta la variable de entorno $name — no se puede continuar sin infraestructura real."
    echo "   Ver specs/002-fase5-deploy-gcp/solicitud-recursos-cloud-run.md para qué falta pedir."
    exit 1
  fi
}

build_and_push() {
  local dockerfile="$1" image="$2" context="$3"
  echo "→ Build + push de imagen: $image"
  # gcloud builds submit --tag solo soporta un Dockerfile en la raíz del contexto ("docker build
  # -t $TAG ." fijo, sin -f). Como el Dockerfile de la API vive en src/MPM.Api/Dockerfile (no en
  # la raíz del repo), armamos un cloudbuild.yaml efímero que sí permite pasar -f.
  local cloudbuild_config
  cloudbuild_config="$(mktemp -t mpm-cloudbuild-XXXXXX.yaml)"
  cat > "$cloudbuild_config" <<EOF
steps:
  - name: 'gcr.io/cloud-builders/docker'
    args: ['build', '-f', '${dockerfile}', '-t', '${image}', '.']
images: ['${image}']
EOF
  gcloud builds submit --project="$GCP_PROJECT" --config="$cloudbuild_config" "$context"
  rm -f "$cloudbuild_config"
}

# App envs comunes a api y a los jobs (comparten el mismo binario MPM.Api, ver Program.cs modo worker).
# La connection string de Postgres NO se compone acá desde piezas (Cloud Run no permite
# interpolar una env var dentro de otra) — vive completa como un solo secreto
# (SECRET_DB_CONNSTRING, ver common_app_secrets), armado por scripts/setup-secrets.sh.
common_app_env_vars() {
  require_var REDIS_HOST
  # CORS_ALLOWED_ORIGINS (QA BUG-011): dominio real de mpm-web en Cloud Run (o el dominio
  # custom, si se configura uno). Sin setear, cae al default de Program.cs
  # (http://localhost:3000,http://localhost:8181) — el frontend de producción quedaría
  # bloqueado por CORS. Se conoce recién después del primer `deploy_web`; en el primer
  # despliegue, correr `deploy_api` de nuevo una vez que `deploy_web` imprima la URL final.
  local cors_origins="${CORS_ALLOWED_ORIGINS:-http://localhost:3000,http://localhost:8181}"
  # 024-inteligencia-competencia-alertas / US3: canal de correo (Brevo provisional, ver
  # memoria de sesion project_brevo_smtp_config) -- defaults reflejan lo ya configurado a mano
  # en Cloud Run el 2026-07-10, para que una corrida futura del script no lo pise con valores
  # distintos ni lo borre (aprendido de un incidente real ese mismo dia: --set-env-vars
  # reemplaza TODO el set, no solo agrega).
  local smtp_host="${SMTP_HOST:-smtp-relay.brevo.com}"
  local smtp_port="${SMTP_PORT:-587}"
  local smtp_username="${SMTP_USERNAME:-66dcf8001@smtp-brevo.com}"
  local smtp_from_email="${SMTP_FROM_EMAIL:-alertas@31032005.xyz}"
  local smtp_from_name="${SMTP_FROM_NAME:-TIVIT Mercado Publico}"
  local smtp_enable_ssl="${SMTP_ENABLE_SSL:-true}"
  # Delimitador "##" en vez de "," -- Cors__AllowedOrigins trae varios dominios separados por
  # comas, y gcloud --set-env-vars usa "," como separador de pares por defecto: con el delimitador
  # default, gcloud intenta parsear cada dominio extra como un par KEY=VALUE y falla con
  # "Bad syntax for dict arg" (encontrado en vivo el 2026-07-10). Los call-sites deben prefijar
  # el valor completo con "^##^" (ver `gcloud topic escaping`) para que esto funcione.
  echo "ASPNETCORE_URLS=http://+:80##ConnectionStrings__Redis=${REDIS_HOST}:${REDIS_PORT}##Storage__Provider=gcs##Storage__Bucket=${GCS_BUCKET}##GOOGLE_CLOUD_PROJECT=${GCP_PROJECT}##Vertex__Region=${GCP_REGION}##JWT__Issuer=TIVIT.MPM##JWT__Audience=MPM.Users##Cors__AllowedOrigins=${cors_origins}##Telegram__BotUsername=${TELEGRAM_BOT_USERNAME}##Smtp__Host=${smtp_host}##Smtp__Port=${smtp_port}##Smtp__Username=${smtp_username}##Smtp__FromEmail=${smtp_from_email}##Smtp__FromName=${smtp_from_name}##Smtp__EnableSsl=${smtp_enable_ssl}"
}

# Telegram__BotToken/WebhookSecret opcionales -- si no existen todavía en Secret Manager (p.ej.
# el primer deploy antes de correr setup-secrets.sh con esos valores), no se agregan al
# --set-secrets para no romper el deploy; Alertas simplemente no manda a Telegram hasta que
# existan (ver TelegramNotificationService, falla aislada; TelegramWebhookController fail-closed).
common_app_secrets() {
  local secrets="JWT__Secret=${SECRET_JWT}:latest,MP_TICKET=${SECRET_MP_TICKET}:latest,ConnectionStrings__PostgreSQL=${SECRET_DB_CONNSTRING}:latest,DB_HOST=db-host:latest,DB_PORT=db-port:latest,DB_NAME=db-name:latest,DB_USER=db-user:latest,DB_PASSWORD=db-password:latest"
  if gcloud secrets describe "$SECRET_TELEGRAM_BOT_TOKEN" --project="$GCP_PROJECT" >/dev/null 2>&1; then
    secrets="${secrets},Telegram__BotToken=${SECRET_TELEGRAM_BOT_TOKEN}:latest"
  fi
  if gcloud secrets describe "$SECRET_TELEGRAM_WEBHOOK_SECRET" --project="$GCP_PROJECT" >/dev/null 2>&1; then
    secrets="${secrets},Telegram__WebhookSecret=${SECRET_TELEGRAM_WEBHOOK_SECRET}:latest"
  fi
  local secret_smtp="${SECRET_SMTP_PASSWORD:-smtp-password}"
  if gcloud secrets describe "$secret_smtp" --project="$GCP_PROJECT" >/dev/null 2>&1; then
    secrets="${secrets},Smtp__Password=${secret_smtp}:latest"
  fi
  echo "$secrets"
}

deploy_api() {
  build_and_push src/MPM.Api/Dockerfile "$API_IMAGE" .
  echo "→ gcloud run deploy $RUN_API_SERVICE"
  # --no-cpu-throttling: mitigación de piso para BUG-002 (QA) — Cloud Run por defecto retira
  # la CPU a una instancia entre peticiones, matando el Task.Run fire-and-forget del análisis
  # IA. El fix estructural es AnalisisRecoveryWorker (ver research.md R2); este flag evita que
  # la mayoría de los análisis normales dependan de que el worker de recuperación los reintente.
  # RUN_INPROCESS_WORKERS=false: el servicio web NO debe correr SyncEngineService/
  # ScraperBackgroundService/AclaracionMonitorService por su cuenta — esos ya corren como
  # Cloud Run Jobs dedicados (sync-job, scraper-job); dejarlos activos acá duplicaba el
  # trabajo (QA BUG-004). En Docker Compose local la variable queda sin setear y el default
  # (true) preserva el comportamiento actual.
  gcloud run deploy "$RUN_API_SERVICE" \
    --project="$GCP_PROJECT" \
    --region="$GCP_REGION" \
    --image="$API_IMAGE" \
    --service-account="$RUN_SERVICE_SA" \
    --network="$VPC_NETWORK" \
    --subnet="$VPC_SUBNET" \
    --vpc-egress=private-ranges-only \
    --min-instances=1 \
    --no-cpu-throttling \
    --allow-unauthenticated \
    --port=80 \
    --set-env-vars="^##^RUN_INPROCESS_WORKERS=false##$(common_app_env_vars)" \
    --set-secrets="$(common_app_secrets)"
}

deploy_web() {
  local api_url
  api_url=$(gcloud run services describe "$RUN_API_SERVICE" --project="$GCP_PROJECT" --region="$GCP_REGION" --format="value(status.url)" 2>/dev/null) || true
  if [ -z "$api_url" ]; then
    echo "❌ No se encontró el servicio $RUN_API_SERVICE — desplegalo primero (scripts/deploy.sh prod api up)"
    exit 1
  fi
  echo "→ API URL detectada: $api_url"

  # Dockerfile relativo al CONTEXTO (src/mpm-web), no al repo -- dentro del contexto subido a
  # Cloud Build, el Dockerfile ya vive en la raíz. Pasar "src/mpm-web/Dockerfile" aquí buscaba
  # "src/mpm-web/src/mpm-web/Dockerfile" y fallaba (encontrado en vivo el 2026-07-10).
  build_and_push Dockerfile "$WEB_IMAGE" src/mpm-web
  echo "→ gcloud run deploy $RUN_WEB_SERVICE"
  gcloud run deploy "$RUN_WEB_SERVICE" \
    --project="$GCP_PROJECT" \
    --region="$GCP_REGION" \
    --image="$WEB_IMAGE" \
    --allow-unauthenticated \
    --set-env-vars="API_URL=${api_url}"
}

deploy_job() {
  local job_name="$1" worker_mode="$2"
  build_and_push src/MPM.Api/Dockerfile "$API_IMAGE" .
  echo "→ gcloud run jobs deploy $job_name (WORKER_MODE=$worker_mode)"
  gcloud run jobs deploy "$job_name" \
    --project="$GCP_PROJECT" \
    --region="$GCP_REGION" \
    --image="$API_IMAGE" \
    --service-account="$RUN_JOBS_SA" \
    --network="$VPC_NETWORK" \
    --subnet="$VPC_SUBNET" \
    --vpc-egress=private-ranges-only \
    --set-env-vars="^##^WORKER_MODE=${worker_mode}##$(common_app_env_vars)" \
    --set-secrets="$(common_app_secrets)" \
    --max-retries=1
}

job_para_scope() {
  case "$1" in
    sync-job)     echo "sync" ;;
    scraper-job)  echo "scraper" ;;
    *) echo "" ;;
  esac
}

run_prod() {
  case "$SCOPE" in
    all)
      case "$CMD" in
        status)
          gcloud run services describe "$RUN_API_SERVICE" --project="$GCP_PROJECT" --region="$GCP_REGION" --format="value(status.conditions)" || true
          gcloud run services describe "$RUN_WEB_SERVICE" --project="$GCP_PROJECT" --region="$GCP_REGION" --format="value(status.url)" || true
          for job in sync-job scraper-job analisis-job; do
            echo "-- $job --"
            gcloud run jobs executions list --job "$job" --project="$GCP_PROJECT" --region="$GCP_REGION" --limit=3 || true
          done
          ;;
        up)
          deploy_api
          deploy_web
          deploy_job sync-job sync
          deploy_job scraper-job scraper
          echo "⚠️ analisis-job NO se despliega automáticamente todavía — AnalisisBackgroundService"
          echo "   no está rediseñado como consumidor de Job/Pub-Sub (ver research.md). Pendiente."
          ;;
        *) echo "Comando '$CMD' no soportado para scope 'all'"; usage ;;
      esac
      ;;
    api)
      case "$CMD" in
        up) deploy_api ;;
        logs) gcloud run services logs read "$RUN_API_SERVICE" --project="$GCP_PROJECT" --region="$GCP_REGION" --limit=100 ;;
        status) gcloud run services describe "$RUN_API_SERVICE" --project="$GCP_PROJECT" --region="$GCP_REGION" ;;
        *) echo "Comando desconocido: $CMD"; usage ;;
      esac
      ;;
    web)
      case "$CMD" in
        up) deploy_web ;;
        logs) gcloud run services logs read "$RUN_WEB_SERVICE" --project="$GCP_PROJECT" --region="$GCP_REGION" --limit=100 ;;
        status) gcloud run services describe "$RUN_WEB_SERVICE" --project="$GCP_PROJECT" --region="$GCP_REGION" ;;
        *) echo "Comando desconocido: $CMD"; usage ;;
      esac
      ;;
    sync-job|scraper-job)
      local worker_mode
      worker_mode=$(job_para_scope "$SCOPE")
      case "$CMD" in
        up) deploy_job "$SCOPE" "$worker_mode" ;;
        execute) gcloud run jobs execute "$SCOPE" --project="$GCP_PROJECT" --region="$GCP_REGION" --wait ;;
        logs) gcloud run jobs executions list --job "$SCOPE" --project="$GCP_PROJECT" --region="$GCP_REGION" --limit=5 ;;
        status) gcloud run jobs describe "$SCOPE" --project="$GCP_PROJECT" --region="$GCP_REGION" ;;
        *) echo "Comando desconocido: $CMD"; usage ;;
      esac
      ;;
    analisis-job)
      echo "⚠️ analisis-job todavía no existe: AnalisisBackgroundService es un disparo"
      echo "   fire-and-forget dentro del proceso web (no un ciclo Timer), no encaja en el"
      echo "   mismo mecanismo WORKER_MODE que sync/scraper sin rediseñarlo primero."
      echo "   Ver specs/002-fase5-deploy-gcp/research.md."
      exit 1
      ;;
    *) usage ;;
  esac
}

case "$ENV" in
  dev)  run_dev ;;
  prod) run_prod ;;
  *) usage ;;
esac
