#!/usr/bin/env bash
# Deploy/gestión de MPM — local (dev) o VM de producción en GCP (prod).
#
# Uso:
#   scripts/deploy.sh <env> <scope> [comando]
#
#   env:     dev | prod
#   scope:   all | api | web | redis | cloudsql-proxy
#   comando: up (default) | down | restart | logs | status | build
#
# Ejemplos:
#   scripts/deploy.sh dev all              # levanta todo el stack local
#   scripts/deploy.sh dev api logs         # sigue los logs del contenedor api local
#   scripts/deploy.sh prod all             # deploy completo en la VM de producción (git pull + up --build -d)
#   scripts/deploy.sh prod web             # redeploy solo del frontend en producción
#   scripts/deploy.sh prod all status      # docker compose ps en la VM
#
# Variables de entorno para prod (con defaults acorde a research.md — sobreescribibles):
#   GCP_PROJECT   (default: tivit-cu010)
#   GCP_ZONE      (default: us-central1-a)
#   GCP_VM_NAME   (default: mpm-prod)

set -euo pipefail

ENV="${1:-}"
SCOPE="${2:-all}"
CMD="${3:-up}"

GCP_PROJECT="${GCP_PROJECT:-tivit-cu010}"
GCP_ZONE="${GCP_ZONE:-us-central1-a}"
GCP_VM_NAME="${GCP_VM_NAME:-mpm-prod}"

usage() {
  echo "Uso: scripts/deploy.sh <dev|prod> <all|api|web|redis|cloudsql-proxy> [up|down|restart|logs|status|build]"
  exit 1
}

[ -z "$ENV" ] && usage

service_flag() {
  # "all" = todos los servicios (sin filtro); cualquier otro valor filtra al servicio nombrado
  if [ "$SCOPE" = "all" ]; then
    echo ""
  else
    echo "$SCOPE"
  fi
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

run_prod() {
  local svc remote_cmd
  svc=$(service_flag)
  case "$CMD" in
    up)
      remote_cmd="cd ~/mpm && git pull && docker compose -f docker-compose.prod.yml up --build -d $svc"
      ;;
    down)
      remote_cmd="cd ~/mpm && docker compose -f docker-compose.prod.yml down $svc"
      ;;
    restart)
      remote_cmd="cd ~/mpm && docker compose -f docker-compose.prod.yml restart $svc"
      ;;
    logs)
      remote_cmd="cd ~/mpm && docker compose -f docker-compose.prod.yml logs -f --tail=200 $svc"
      ;;
    status)
      remote_cmd="cd ~/mpm && docker compose -f docker-compose.prod.yml ps"
      ;;
    build)
      remote_cmd="cd ~/mpm && docker compose -f docker-compose.prod.yml build $svc"
      ;;
    *) echo "Comando desconocido: $CMD"; usage ;;
  esac

  echo "→ Ejecutando en $GCP_VM_NAME ($GCP_ZONE, proyecto $GCP_PROJECT):"
  echo "  $remote_cmd"
  gcloud compute ssh "$GCP_VM_NAME" \
    --zone="$GCP_ZONE" \
    --project="$GCP_PROJECT" \
    --command="$remote_cmd"
}

case "$ENV" in
  dev)  run_dev ;;
  prod) run_prod ;;
  *) usage ;;
esac
