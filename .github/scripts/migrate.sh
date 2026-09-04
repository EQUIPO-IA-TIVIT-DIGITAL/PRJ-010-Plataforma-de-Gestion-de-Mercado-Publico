#!/bin/sh
# Aplica las migraciones V*.sql en una BD Postgres fresca, con la misma semántica
# que MPM.Api DatabaseInitializer: orden por nombre, registro en _migrations,
# aborta al primer error (ON_ERROR_STOP).
# Uso CI: PG* env + volumen /scripts. Uso local (git-bash/WSL):
#   PGHOST=localhost PGPORT=5433 PGDATABASE=mpm PGUSER=mpm PGPASSWORD=mpm_password \
#     sh .github/scripts/migrate.sh ./src/MPM.Api/Database/Scripts
set -eu
SCRIPTS_DIR="${1:-/scripts}"
export PGHOST="${PGHOST:-localhost}"
export PGPORT="${PGPORT:-5433}"
export PGDATABASE="${PGDATABASE:-mpm}"
export PGUSER="${PGUSER:-mpm}"

psql -v ON_ERROR_STOP=1 -c "CREATE TABLE IF NOT EXISTS _migrations (version VARCHAR(50) PRIMARY KEY, applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP)"

# shellcheck disable=SC2012
for f in $(ls "$SCRIPTS_DIR"/V*.sql | sort); do
  v=$(basename "$f" | grep -oE 'V[0-9]{3}' | head -n 1)
  already=$(psql -tAc "SELECT version FROM _migrations WHERE version = '$v'")
  if [ -z "$already" ]; then
    echo "Applying $v ($f)"
    psql -v ON_ERROR_STOP=1 -f "$f"
    psql -v ON_ERROR_STOP=1 -c "INSERT INTO _migrations (version) VALUES ('$v')"
  else
    echo "Skipping $v (already applied)"
  fi
done
echo "Migrations OK"
