#!/usr/bin/env bash
# Restore de mpm-db (Cloud SQL) desde un backup en GCS (creado por backup-db.sh o por un
# backup automático diario exportado manualmente). Ver Escenario 5 de quickstart.md —
# debe completarse en menos de 30 minutos (SC-004).
#
# ⚠️ Por defecto NO apunta a la instancia productiva — exige --target explícito para
# evitar sobrescribir mpm-db por error. Usar una instancia de prueba/clone salvo que se
# esté haciendo un restore real de incidente, coordinado explícitamente.
#
# Uso: scripts/restore-db.sh <gs://ruta-al-backup.sql.gz> --target <instancia-cloud-sql>
#
# Variables de entorno:
#   GCP_PROJECT (default: tivit-cu010)

set -euo pipefail

GCP_PROJECT="${GCP_PROJECT:-tivit-cu010}"
BACKUP_PATH="${1:-}"
TARGET_FLAG="${2:-}"
TARGET_INSTANCE="${3:-}"

if [ -z "$BACKUP_PATH" ] || [ "$TARGET_FLAG" != "--target" ] || [ -z "$TARGET_INSTANCE" ]; then
  echo "Uso: scripts/restore-db.sh <gs://ruta-al-backup.sql.gz> --target <instancia-cloud-sql>"
  echo "Ejemplo: scripts/restore-db.sh gs://tivit-cu010-mpm-adjuntos/backups/mpm-backup-20260706.sql.gz --target mpm-db-restore-test"
  exit 1
fi

echo "→ Restaurando ${BACKUP_PATH} en la instancia ${TARGET_INSTANCE} (proyecto ${GCP_PROJECT})"
echo "  Esto SOBRESCRIBE la base 'mpm' de la instancia destino. Confirmá que es la correcta."
read -r -p "Continuar? [s/N] " confirm
if [ "$confirm" != "s" ] && [ "$confirm" != "S" ]; then
  echo "Cancelado."
  exit 1
fi

START=$(date +%s)

gcloud sql import sql "$TARGET_INSTANCE" "$BACKUP_PATH" \
  --project="$GCP_PROJECT" \
  --database=mpm \
  --quiet

END=$(date +%s)
ELAPSED=$((END - START))

echo "→ Restore completado en ${ELAPSED}s ($((ELAPSED / 60)) min)"
if [ "$ELAPSED" -gt 1800 ]; then
  echo "⚠️ Superó los 30 minutos (SC-004) — revisar tamaño de la base o tier de la instancia destino."
fi
