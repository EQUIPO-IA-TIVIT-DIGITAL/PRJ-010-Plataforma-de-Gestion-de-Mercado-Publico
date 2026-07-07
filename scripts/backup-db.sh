#!/usr/bin/env bash
# Backup manual de mpm-db (Cloud SQL) hacia GCS, complementario a los backups automáticos
# diarios ya activos en la instancia (ver research.md de 002-fase5-deploy-gcp).
#
# Uso: scripts/backup-db.sh [nombre-archivo]
#
# Variables de entorno:
#   GCP_PROJECT     (default: tivit-cu010)
#   CLOUDSQL_INSTANCE (default: mpm-db)
#   BACKUP_BUCKET   (default: tivit-cu010-mpm-adjuntos, bajo el prefijo backups/)

set -euo pipefail

GCP_PROJECT="${GCP_PROJECT:-tivit-cu010}"
CLOUDSQL_INSTANCE="${CLOUDSQL_INSTANCE:-mpm-db}"
BACKUP_BUCKET="${BACKUP_BUCKET:-tivit-cu010-mpm-adjuntos}"

TIMESTAMP=$(date -u +%Y%m%d-%H%M%S)
FILE_NAME="${1:-mpm-backup-${TIMESTAMP}.sql.gz}"
GCS_PATH="gs://${BACKUP_BUCKET}/backups/${FILE_NAME}"

echo "→ Exportando ${CLOUDSQL_INSTANCE} (proyecto ${GCP_PROJECT}) a ${GCS_PATH}"

# gcloud sql export requiere que la Service Account de Cloud SQL tenga permiso de escritura
# sobre el bucket destino (roles/storage.objectAdmin scopeado, no un rol de proyecto amplio).
gcloud sql export sql "$CLOUDSQL_INSTANCE" "$GCS_PATH" \
  --project="$GCP_PROJECT" \
  --database=mpm \
  --offload

echo "→ Backup completado: ${GCS_PATH}"
