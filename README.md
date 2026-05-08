# MealPrepDB

SQL Server database for a meal-prep / recipe-tracking app. Built as a school practica project. Runs in Docker, exposes a stored-proc API, and uses a least-privilege SQL login so the .NET app cannot touch tables directly.

The .NET app side is intentionally out of scope here — this repo is the database half.

## What's in it

- **8 tables** — 6 core (Users, Units, Categories, Ingredients, Recipes, RecipeIngredients) + 2 for security/auditing (PasswordHistory, AuditLog).
- **18 stored procedures** — the only API surface the app sees. Covers register/login, password change with history check, recipe CRUD, paged search, find-recipes-by-ingredients, and lookup reads.
- **Lockout policy** — 5 failed logins in a row → 15-minute lockout. Last 5 password hashes retained per user; reuse is rejected.
- **Audit log** — every mutating proc writes a row in the same transaction.
- **Least-privilege app role** — `mealprep_app` has `EXECUTE` on the `dbo` schema and is **denied** SELECT/INSERT/UPDATE/DELETE. Mutations work only through stored procs via SQL Server ownership chaining.

## Repo layout

```
Database/
├── 00_create_database.sql        Phase 1: schema
├── 01_users.sql … 06_recipe_ingredients.sql
├── 07_users_security.sql         Phase 2: security state
├── 08_audit_log.sql              audit log + IntList TVP
├── 09_app_role.sql               app login, role, GRANT/DENY
├── procs/                        the stored-proc API (5 files, 18 procs)
├── seeds/                        units + categories seed data
└── run_all.sql                   master script (idempotent)

Vault/                            Obsidian notes — decisions log, per-table notes, sessions
CLAUDE.md                         working instructions for Claude Code
```

## Running it

Start a SQL Server 2022 container:

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=<your-sa-password>" \
  -p 1433:1433 --name MealPrepDB \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

Build the schema (idempotent — safe to re-run; pass the password you want for the `mealprep_app` login):

```bash
docker cp Database MealPrepDB:/tmp/Database
docker exec -w /tmp/Database MealPrepDB /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$(docker exec MealPrepDB printenv SA_PASSWORD)" \
  -C -b -i run_all.sql -v AppPassword="<app-login-password>"
```

Notes:
- `mssql-tools18` requires `-C` (TLS is enforced by default).
- `-b` makes sqlcmd exit non-zero on T-SQL errors — use it in scripts.
- `run_all.sql` uses `:r` includes, so it must run via sqlcmd, not via a generic JDBC client.

Run a one-off query:

```bash
docker exec MealPrepDB /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$(docker exec MealPrepDB printenv SA_PASSWORD)" \
  -C -d MealPrepDB -h-1 -W -Q "SELECT name FROM sys.procedures ORDER BY name;"
```

## App connection string

The .NET app should connect as `mealprep_app`, never `sa`:

```
Server=localhost,1433;Database=MealPrepDB;User Id=mealprep_app;Password=<app-login-password>;TrustServerCertificate=true;
```

## API surface

| Area | Procs |
|---|---|
| Users / auth | `sp_RegisterUser`, `sp_GetUserForLogin`, `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword` |
| Recipes (write) | `sp_CreateRecipe`, `sp_UpdateRecipe`, `sp_DeleteRecipe` |
| Recipes (read) | `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients` |
| Ingredients | `sp_AddIngredient`, `sp_GetIngredients`, `sp_SearchIngredients` |
| Lookups | `sp_GetUnits`, `sp_GetCategories` |
| Internal | `sp_WriteAudit` |

`sp_FindRecipesByIngredients` takes a `dbo.IntList` table-valued parameter so the app can pass a list of ingredient IDs cleanly. `sp_CreateRecipe` / `sp_UpdateRecipe` accept the ingredient list as JSON (parsed via `OPENJSON`).

Custom error codes raised by procs:

- `50001` — password reused (rejected by `sp_ChangePassword`)
- `50002` — not authorized (e.g. trying to update someone else's recipe)
- `50003` — not found

## Design notes

A few non-obvious decisions, all recorded with full rationale in `Vault/Decisions Log.md`:

- **Stored-proc-only API** — the app has zero direct table access. Makes SQL injection structurally impossible from the app side.
- **Hard delete, not soft** — v1 scope; no `IsDeleted` flags.
- **Cascade is rare on purpose** — only `Recipes → RecipeIngredients` and `Users → PasswordHistory`. Deleting a user with recipes is blocked, deliberately.
- **All timestamps UTC** via `SYSUTCDATETIME()`. Display conversion is the app's job.
- **`NVARCHAR` everywhere** (Unicode), never `VARCHAR`.
- **Idempotent scripts** — `IF OBJECT_ID(...) IS NULL`, `IF COL_LENGTH(...) IS NULL`, `MERGE` for seeds. Re-running `run_all.sql` never destroys local data.
- **Constraint naming** — `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_` prefixes, explicit names (auto-generated names are unstable and unreadable in error messages).

## Out of scope for v1

Meal plans, shopping lists, nutrition, ratings, photos, tags, full-text search, soft delete, temporal tables, email verification.
