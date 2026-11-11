#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TEST_CONTAINER="mealprep-api-test-$(date +%s)-$$"
TEST_PASSWORD="Test-$(openssl rand -hex 16)!"
cleanup(){ docker rm -f "$TEST_CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT
# Always a fresh container and loopback-only ephemeral port; never a live database.
docker run -d --name "$TEST_CONTAINER" -e ACCEPT_EULA=Y -e MSSQL_PID=Developer -e MSSQL_SA_PASSWORD="$TEST_PASSWORD" -p 127.0.0.1::1433 -v "$REPO_ROOT/Database:/schema:ro" mcr.microsoft.com/mssql/server:2022-latest >/dev/null
sql(){ docker exec -e SQLCMDPASSWORD="$TEST_PASSWORD" -w /schema "$TEST_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b "$@"; }
ready=false
for attempt in {1..60};do if sql -Q 'SELECT 1' >/dev/null 2>&1;then ready=true;break;fi;sleep 2;done
if [ "$ready" != true ];then echo 'Disposable SQL Server did not start' >&2;exit 1;fi
sql -v AppPassword="$TEST_PASSWORD" -i run_all.sql >/dev/null
sql -i migrations/20261111_companion.sql >/dev/null
TEST_PORT="$(docker port "$TEST_CONTAINER" 1433/tcp | sed 's/.*://')"
export MEALPREP_TEST_CONNECTION="Server=127.0.0.1,$TEST_PORT;Database=MealPrepDB;User ID=mealprep_app;Password=$TEST_PASSWORD;Encrypt=True;TrustServerCertificate=True"
export MEALPREP_TEST_CONTAINER="$TEST_CONTAINER"
dotnet run --project "$REPO_ROOT/Tests/MealPrepApp.ApiChecks" -c Release --no-build
