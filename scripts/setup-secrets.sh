#!/usr/bin/env bash
# Crea/actualiza en Secret Manager los secretos que scripts/deploy.sh monta en Cloud Run.
# Idempotente: si el secreto no existe lo crea, si existe agrega una nueva versión.
#
# ⚠️ Este script NO se corrió todavía — requiere valores reales de producción que no se
# generan solos:
#   - JWT_SECRET: debe ser NUEVO, no reusar el de dev/staging (rotación explícita, ver
#     specs/002-fase5-deploy-gcp/research.md §6 y plan.md "Información requerida").
#   - MP_TICKET: el mismo que ya usa el sistema (o nuevo si se rota).
#   - Connection string de Postgres: requiere CLOUDSQL_PRIVATE_IP real, que depende de que
#     Nicolás termine de mover mpm-db a la VPC custom (hoy la IP privada existente,
#     10.33.176.3, está en la VPC default y puede cambiar al re-peerear).
#
# Gemini ya NO necesita secreto (020-migracion-gemini-adc) — se autentica vía ADC con la
# identidad de mpm-api-sa/mpm-jobs-sa (roles/aiplatform.user), nativo en Cloud Run.
#
# Uso:
#   JWT_SECRET=... MP_TICKET=... \
#   DB_USER=... DB_PASSWORD=... CLOUDSQL_PRIVATE_IP=... DB_NAME=mpm \
#   scripts/setup-secrets.sh

set -euo pipefail

GCP_PROJECT="${GCP_PROJECT:-tivit-cu010}"
DB_NAME="${DB_NAME:-mpm}"

require_var() {
  local name="$1"
  if [ -z "${!name:-}" ]; then
    echo "❌ Falta la variable $name. Ver el encabezado de este script para la lista completa."
    exit 1
  fi
}

require_var JWT_SECRET
require_var MP_TICKET
require_var DB_USER
require_var DB_PASSWORD
require_var CLOUDSQL_PRIVATE_IP
# TELEGRAM_BOT_TOKEN es opcional — si no se define, Telegram simplemente no envía (ver
# TelegramNotificationService, falla de forma aislada sin bloquear la notificación in-app)
TELEGRAM_BOT_TOKEN="${TELEGRAM_BOT_TOKEN:-}"
# TELEGRAM_WEBHOOK_SECRET: requerido para que "Conectar con Telegram" (deep-link) funcione en
# prod -- el webhook es fail-closed (QA BUG-009), sin este secreto el endpoint rechaza todo con
# 401 y el linking automático nunca completa (el fallback manual de Chat ID sigue funcionando
# igual). Generarlo random si no se pasa.
TELEGRAM_WEBHOOK_SECRET="${TELEGRAM_WEBHOOK_SECRET:-}"

CONNSTRING="Host=${CLOUDSQL_PRIVATE_IP};Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD}"

upsert_secret() {
  local name="$1" value="$2"
  if gcloud secrets describe "$name" --project="$GCP_PROJECT" >/dev/null 2>&1; then
    echo "→ Actualizando secreto existente: $name"
    printf '%s' "$value" | gcloud secrets versions add "$name" --project="$GCP_PROJECT" --data-file=-
  else
    echo "→ Creando secreto nuevo: $name"
    printf '%s' "$value" | gcloud secrets create "$name" --project="$GCP_PROJECT" --replication-policy=automatic --data-file=-
  fi
}

upsert_secret "jwt-secret" "$JWT_SECRET"
upsert_secret "mp-ticket" "$MP_TICKET"
upsert_secret "postgresql-connection-string" "$CONNSTRING"
if [ -n "$TELEGRAM_BOT_TOKEN" ]; then
  upsert_secret "telegram-bot-token" "$TELEGRAM_BOT_TOKEN"
fi
if [ -n "$TELEGRAM_WEBHOOK_SECRET" ]; then
  upsert_secret "telegram-webhook-secret" "$TELEGRAM_WEBHOOK_SECRET"
fi

# El subproceso Node del scraper (tools/scraper-mp/) no lee ConnectionStrings__PostgreSQL —
# necesita host/puerto/usuario/password sueltos (QA BUG-005: antes DB_HOST venía hardcodeado
# a "db", el nombre del servicio de Docker Compose local, que no resuelve en Cloud Run).
upsert_secret "db-host" "$CLOUDSQL_PRIVATE_IP"
upsert_secret "db-port" "5432"
upsert_secret "db-name" "$DB_NAME"
upsert_secret "db-user" "$DB_USER"
upsert_secret "db-password" "$DB_PASSWORD"

echo "✅ Secretos listos. Confirmar que mpm-api-sa y mpm-jobs-sa tengan roles/secretmanager.secretAccessor"
echo "   (bloqueado por permisos hoy — ver specs/002-fase5-deploy-gcp/solicitud-recursos-cloud-run.md)."
