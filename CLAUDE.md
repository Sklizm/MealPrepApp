# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repo layout

The whole meal-prep project lives here — database and app together.

- `Database/` — T-SQL for SQL Server in a Docker container. The schema, stored procs, and seeds.
- `App/` — WPF .NET app (`MealPrepApp/`) that talks to the database via the `mealprep_app` proc-only login. `legacy-winforms/` is the earlier WinForms prototype, kept for reference. `appsettings.Local.json` holds the app password and is gitignored — never commit it.
- `Vault/` — Obsidian vault that is the **explicit cross-session source of truth**. Read it at the start of any new session and update it at the end (see "Resume protocol" below).

## Running the database

The container is named `MealPrepDB` (image `mcr.microsoft.com/mssql/server:2022-latest`, port 1433). The SA password lives only in the container env (`SA_PASSWORD`).

Run the full schema build (idempotent — safe to re-run):

```bash
docker cp Database MealPrepDB:/tmp/Database
docker exec -w /tmp/Database MealPrepDB /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$(docker exec MealPrepDB printenv SA_PASSWORD)" \
  -C -b -i run_all.sql
```

Run a one-off query against the existing DB:

```bash
docker exec MealPrepDB /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$(docker exec MealPrepDB printenv SA_PASSWORD)" \
  -C -d MealPrepDB -h-1 -W -Q "SELECT name FROM sys.tables ORDER BY name;"
```

Notes:
- `mssql-tools18` (not `mssql-tools`) is what's installed; it requires `-C` (trust server cert) because TLS is enforced by default.
- `-b` makes sqlcmd exit non-zero on T-SQL errors — always use it in scripts.
- `run_all.sql` uses sqlcmd `:r` includes, so it must be run with sqlcmd, not via JDBC/DataGrip's regular console (DataGrip needs each file run individually unless its sqlcmd mode is enabled).

DataGrip is the user's normal GUI client; sqlcmd is the scriptable path Claude should default to.

## Schema architecture

Database name: `MealPrepDB`. 12 tables: six core (Users, Units, Categories, Ingredients, Recipes, RecipeIngredients) plus security/audit (PasswordHistory, AuditLog), meal planning (MealPlanEntries, RecipeFavorites, UserPantry), and a lookup (IngredientCategories). The build order below covers the six core tables; the rest are added in `11_`–`14_` and the security/audit scripts. Rationale for every table lives in `Vault/Decisions Log.md`.

Build order (also encoded in `Database/run_all.sql`):

```
00_create_database  →  01_users  →  02_units  →  03_categories
                                          ↓             ↓
                                   04_ingredients      ↓
                                          ↓             ↓
                                   05_recipes ←────────┘
                                          ↓
                                   06_recipe_ingredients
                            +  seeds/{units,categories}_seed
```

Cascade/RESTRICT design (intentional — only one cascade in the entire schema):

- `Recipes → RecipeIngredients`: `ON DELETE CASCADE` (RI rows have no meaning without their recipe).
- Everything else is RESTRICT. Deleting a User with recipes, an Ingredient in use, or a Unit in use is blocked. This is by design — see Decisions Log.

Other load-bearing design choices (don't change without checking the Decisions Log):

- **Ingredients are global** — no `UserID` column in v1. If user-private ingredients are ever needed, add a nullable `UserID` (`NULL` = global).
- **All timestamps UTC** via `SYSUTCDATETIME()` defaults. The .NET app converts for display.
- **Idempotent scripts**: every `CREATE TABLE` wrapped in `IF OBJECT_ID(...) IS NULL`; every seed uses `MERGE`. Re-running the build must never destroy local test data.
- **Constraint naming**: `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_` prefixes with explicit names (auto-generated names are unstable across rebuilds and unreadable in error messages).
- **`NVARCHAR` everywhere** (Unicode), never `VARCHAR`.
- **PKs**: `<TableName>ID INT IDENTITY(1,1)`.

When adding a new table or column, follow these conventions and append a Decisions Log entry if any non-trivial choice is made.

## Resume protocol (for cross-session continuity)

The Obsidian vault at `Vault/` is the project's persistent state — Codrin set it up specifically so work survives context windows running out.

**At session start**, read in this order:
1. `Vault/00 - Index.md` (hub)
2. The latest entry in `Vault/Sessions/` (newest = most relevant)
3. `Vault/TODO.md` (what's next)
4. `Vault/Decisions Log.md` (the *why* behind the current shape)

**At session end** (only if meaningful work happened):
- Append a new dated entry under `Vault/Sessions/YYYY-MM-DD - <name>.md`
- Update `Vault/TODO.md` (move done items, add new ones)
- Append (never rewrite) to `Vault/Decisions Log.md` if any non-trivial decision was made
- Per-table notes under `Vault/Database/` should match the SQL — update them when the schema changes

If the vault and the SQL ever disagree, **trust the SQL** and update the vault.
