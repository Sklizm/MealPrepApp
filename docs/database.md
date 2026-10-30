# Database setup and upgrades

These examples use Bash and Docker from the repository root on the database host. SQL Server runs separately from desktop clients. Supply your own administrator and application-login passwords through the environment or interactive prompt.

Docker Compose starts SQL Server; it does not automatically execute `run_all.sql`.

## Existing database

Run the disposable suite first from a Docker-capable shell. After it passes, close desktop clients and apply both updates on the database host:

```bash
bash scripts/test-database.sh &&
bash scripts/update-database.sh mealprep-db &&
bash scripts/update-database.sh mealprep-db 20261028_cooking_redesign.sql
```

Each update creates a copy-only checksum backup, verifies it and copies it to gitignored `backups/` before migration. Backup failure stops the update. These migrations preserve existing users, data and login credentials; the second adds saved shopping lists and week copying. Use this path for an existing deployment. [Migration reference](redesign/README.md#database-rollout).

## New database

Set `MSSQL_SA_PASSWORD` in the deployment environment, then start the container:

```bash
docker compose -f docker-compose.db.yml up -d
```

Once SQL Server is ready, initialize from the mounted schema directory, supplying a separate application-login password. The command below prompts for it and keeps the administrator password inside the container:

```bash
read -r -s -p 'New application-login password: ' MEALPREP_APP_PASSWORD
printf '\n'
printf '%s\n' "$MEALPREP_APP_PASSWORD" |
  docker exec -i -w /docker-entrypoint-initdb.d mealprep-db sh -c '
    IFS= read -r MEALPREP_APP_PASSWORD
    export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"
    exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b \
      -v AppPassword="$MEALPREP_APP_PASSWORD" -i run_all.sql
  '
unset MEALPREP_APP_PASSWORD
```

Use a SQL-policy-compliant password without single quotes or newlines: the initialization script interpolates it into the login-creation SQL. `run_all.sql` includes schema, seeds and procedures; repeatability is covered by the disposable test suite. Existing login passwords are not rotated by initialization. Credential rotation is a separate deployment operation.


Database access uses parameterized stored procedures. Installers configure client connections; they do not create or migrate databases. Keep administrator credentials on the database host and use the application login for desktop clients.
