#!/usr/bin/env bash
# Run on the database host from normal SSH; updates the selected existing container.
set -euo pipefail
umask 077
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DB_CONTAINER="${1:-mealprep-db}"
MIGRATION_FILE="${2:-20261028_compatibility.sql}"
case "$MIGRATION_FILE" in 20261028_compatibility.sql|20261028_cooking_redesign.sql) ;; *) echo "Unsupported migration" >&2; exit 2 ;; esac
MIGRATION_ID="$(date -u +%Y%m%dT%H%M%SZ)-$$"
STAGE_DIR="/tmp/mealprep-compatibility-$MIGRATION_ID"
CONTAINER_BACKUP="/var/opt/mssql/backup/MealPrepDB-before-compatibility-$MIGRATION_ID.bak"
HOST_BACKUP="$REPO_ROOT/backups/$(basename "$CONTAINER_BACKUP")"

cleanup() {
    docker exec "$DB_CONTAINER" rm -rf -- "$STAGE_DIR" >/dev/null 2>&1 || true
}
trap cleanup EXIT

# The administrator password remains inside the container; never print it or put it in argv.
sql() {
    docker exec -i -w "$STAGE_DIR" "$DB_CONTAINER" sh -c '
        export SQLCMDPASSWORD="${MSSQL_SA_PASSWORD:-${SA_PASSWORD:-}}"
        : "${SQLCMDPASSWORD:?Database container does not expose its administrator password}"
        exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b "$@"
    ' sh "$@"
}

docker inspect --format '{{.State.Running}}' "$DB_CONTAINER" | grep -qx true
docker exec "$DB_CONTAINER" mkdir -p "$STAGE_DIR" /var/opt/mssql/backup
docker cp "$REPO_ROOT/Database/." "$DB_CONTAINER:$STAGE_DIR/" >/dev/null

echo "Checking existing MealPrepDB in container $DB_CONTAINER"
sql -Q "IF DB_ID(N'MealPrepDB') IS NULL THROW 51000, N'MealPrepDB does not exist.', 1;"

mkdir -p "$REPO_ROOT/backups"
echo "Creating and verifying a copy-only database backup"
sql -Q "BACKUP DATABASE [MealPrepDB] TO DISK=N'$CONTAINER_BACKUP' WITH COPY_ONLY, CHECKSUM, INIT;
RESTORE VERIFYONLY FROM DISK=N'$CONTAINER_BACKUP' WITH CHECKSUM;"
docker cp "$DB_CONTAINER:$CONTAINER_BACKUP" "$HOST_BACKUP" >/dev/null
test -s "$HOST_BACKUP"
chmod 600 "$HOST_BACKUP"
echo "Verified backup saved to $HOST_BACKUP"

echo "Applying compatibility update (existing users/data and login passwords preserved)"
sql -i "migrations/$MIGRATION_FILE"
echo "Database update completed: $MIGRATION_FILE"
