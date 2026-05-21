# MealPrep — recipe tracking & meal planning

A full meal-prep / recipe-tracking + meal-planning application, built as a school practica project. Two halves, both in this repo:

- **The app** — a Windows desktop client (WPF + MVVM + Dapper, .NET 10), Romanian-language UI, with a cream/olive design system.
- **The database** — SQL Server running in Docker, exposing a stored-proc-only API behind a least-privilege login so the app can never touch tables directly.

The app talks to the database exclusively through stored procedures, authenticated as the low-privilege `mealprep_app` login. The companion design spec lives at [`Vault/Design/App Design Spec.md`](Vault/Design/App%20Design%20Spec.md).

## The app (`App/MealPrepApp/`)

WPF on **.NET 10** (`net10.0-windows`), MVVM via **CommunityToolkit.Mvvm**, data access via **Dapper** over **Microsoft.Data.SqlClient**, DI through **Microsoft.Extensions.DependencyInjection**, config from `appsettings.json` + a gitignored `appsettings.Local.json` (holds the app password). Excel export via **ClosedXML**; password hashing via **BCrypt.Net**.

Screens (Romanian UI):

- **Autentificare** — register, login, change password. Lockout + password-history rules enforced server-side.
- **Acasă** — dashboard with KPI tiles and recent recipes.
- **Rețete** — recipe list, detail, and editor (ingredients entered as a list, optimistic-concurrency-safe save).
- **Ingrediente** — ingredient list (flat or grouped by category) with live as-you-type search, ingredient add; **Frigider** (pantry) add/edit/remove; **Listă de cumpărături** (computed shopping list with date range, Excel export, and print).
- **Planificare** / **Rapoarte** — meal-plan calendar and reports (DB procs ready; UI in progress).

**Design system** — a single cream / olive / dark-brown theme in `Themes/Colors.xaml` + `Themes/Styles.xaml`: chrome-less windows (`WindowChrome`), a styled `MessageDialog` replacing `MessageBox`, and themed date pickers, menus, tooltips, and scrollbars. `appsettings.Local.json`, `bin`/`obj`, and source-snapshot zips stay out of git.

Build + run on Windows with the .NET 10 SDK installed: create `appsettings.Local.json` from the committed template with the real `mealprep_app` password, point the connection string at the running SQL Server, then `dotnet run` (or open the solution in Visual Studio).

## The database — what's in it

- **12 tables** — 6 core (Users, Units, Categories, Ingredients, Recipes, RecipeIngredients) + 2 for security/auditing (PasswordHistory, AuditLog) + 3 for meal planning (MealPlanEntries, RecipeFavorites, UserPantry) + 1 lookup (IngredientCategories).
- **38 stored procedures** — the only API surface the app sees. Covers register/login, password change with history check, recipe CRUD with optimistic concurrency, paged search, find-recipes-by-ingredients, weekly/monthly meal plan reads, favorites toggle, pantry upsert, computed shopping list, dashboard counts, monthly reports, and lookup reads.
- **44 seeded ingredients** (in Romanian, no diacritics) across 8 categories so the app demo isn't staring at an empty list on first launch.
- **Lockout policy** — 5 failed logins in a row → 15-minute lockout. Last 5 password hashes retained per user; reuse is rejected.
- **Optimistic concurrency** on Recipes — `RowVersion` column, `sp_UpdateRecipe` requires `@RowVersion` and `THROW 50004` on stale row.
- **Audit log** — every mutating proc writes a row in the same transaction.
- **Least-privilege app role** — `mealprep_app` has `EXECUTE` on the `dbo` schema and is **denied** SELECT/INSERT/UPDATE/DELETE. Mutations work only through stored procs via SQL Server ownership chaining.

## Repo layout

```
App/
├── MealPrepApp/                  WPF .NET 10 client (Views, ViewModels, Data, Services, Themes)
└── legacy-winforms/              earlier WinForms prototype, kept for reference

Database/
├── 00_create_database.sql        Phase 1: schema
├── 01_users.sql … 06_recipe_ingredients.sql
├── 07_users_security.sql         Phase 2: security state on Users + PasswordHistory
├── 08_audit_log.sql              AuditLog + IntList TVP + sp_WriteAudit
├── 09_app_role.sql               app login, role, GRANT/DENY
├── 10_phase25_additions.sql      Phase 2.5: FK index gaps + RowVersion on Recipes
├── 11_meal_plan.sql              Phase 3: MealPlanEntries
├── 12_favorites.sql              Phase 3: RecipeFavorites
├── 13_pantry.sql                 Phase 3: UserPantry
├── 14_ingredient_categories.sql  Phase 4: IngredientCategories + FK column on Ingredients
├── procs/                        the stored-proc API (11 files, 38 procs)
├── seeds/                        units + categories + ingredients + ingredient_categories
└── run_all.sql                   master script (idempotent)

Vault/                            Obsidian notes — decisions log, per-table notes, design spec, sessions
CLAUDE.md                         working instructions for Claude Code
```

## Running it

Start a SQL Server 2022 container:

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=<your-sa-password>" \
  -p 1433:1433 --name MealPrepDB \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

Build the schema (idempotent — safe to re-run):

```bash
docker exec -u 0 MealPrepDB rm -rf /tmp/Database
docker cp Database MealPrepDB:/tmp/Database
docker exec -w /tmp/Database MealPrepDB /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$(docker exec MealPrepDB printenv SA_PASSWORD)" \
  -C -b -i run_all.sql
```

First-time bring-up only: `09_app_role.sql` needs the password for the `mealprep_app` login. Either edit the `:setvar AppPassword ""` line in that file to your chosen password before the first run, or delete that line and add `-v AppPassword="<your-password>"` to the sqlcmd command above. On rebuilds the login already exists at the server level, so the password is unused and no flag is needed.

Notes:
- The `rm -rf /tmp/Database` step is needed if you've copied before — `docker cp` nests the copy inside the existing dir otherwise. Run as root inside the container (`-u 0`) because the existing files are owned by uid 1000.
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

The .NET app connects as `mealprep_app`, never `sa`:

```
Server=localhost,1433;Database=MealPrepDB;User Id=mealprep_app;Password=<app-login-password>;TrustServerCertificate=true;
```

## API surface

| Area | Procs |
|---|---|
| Users / auth | `sp_RegisterUser`, `sp_GetUserForLogin` (login flow), `sp_GetUserProfile` (profile screen — no PasswordHash), `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword` |
| Recipes (write) | `sp_CreateRecipe`, `sp_UpdateRecipe` (requires `@RowVersion`), `sp_DeleteRecipe` |
| Recipes (read) | `sp_GetRecipeFull` (returns `RowVersion`), `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients` |
| Ingredients | `sp_AddIngredient`, `sp_GetIngredients(@IngredientCategoryID = NULL)`, `sp_SearchIngredients`, `sp_GetIngredientUsage` |
| Lookups | `sp_GetUnits`, `sp_GetCategories`, `sp_GetIngredientCategories` |
| Meal plan | `sp_PlanMeal`, `sp_UpdatePlannedMeal`, `sp_UnplanMeal`, `sp_GetWeeklyPlan`, `sp_GetMonthlyPlan` |
| Favorites | `sp_ToggleFavorite`, `sp_GetFavoriteRecipes` |
| Pantry | `sp_AddPantryItem` (MERGE upsert), `sp_UpdatePantryQuantity`, `sp_RemovePantryItem`, `sp_GetPantry` |
| Shopping list | `sp_GetShoppingList(@UserID, @StartDate, @EndDate)` — computed, joins planned meals minus pantry, scales by serving count |
| Dashboard | `sp_GetDashboardCounts`, `sp_GetRecentRecipes` |
| Reports | `sp_GetMonthlyStats`, `sp_GetTopRecipes`, `sp_GetTopIngredients` |
| Internal | `sp_WriteAudit` (called from every mutating proc) |

`sp_FindRecipesByIngredients` takes a `dbo.IntList` table-valued parameter so the app can pass a list of ingredient IDs cleanly. `sp_CreateRecipe` / `sp_UpdateRecipe` accept the ingredient list as JSON (parsed via `OPENJSON`).

Custom error codes raised by procs:

- `50001` — password reused (rejected by `sp_ChangePassword`)
- `50002` — not authorized (e.g. trying to update someone else's recipe or meal plan entry)
- `50003` — not found
- `50004` — stale row (optimistic concurrency conflict on `sp_UpdateRecipe`)

## Design notes

Load-bearing decisions, all recorded with full rationale in [`Vault/Decisions Log.md`](Vault/Decisions%20Log.md):

- **Stored-proc-only API** — the app has zero direct table access. Makes SQL injection structurally impossible from the app side.
- **Hard delete, not soft** — no `IsDeleted` flags; rows are removed outright.
- **Cascade is rare on purpose** — only `Recipes → RecipeIngredients`, `Recipes → MealPlanEntries`, `Users → PasswordHistory`, `Users/Recipes → RecipeFavorites`, `Users → UserPantry`. Deleting a user with recipes is blocked, deliberately.
- **All timestamps UTC** via `SYSUTCDATETIME()`. Display conversion is the app's job.
- **`NVARCHAR` everywhere** (Unicode), never `VARCHAR`.
- **Idempotent scripts** — `IF OBJECT_ID(...) IS NULL`, `IF COL_LENGTH(...) IS NULL`, `MERGE` for seeds. Re-running `run_all.sql` never destroys local data.
- **Constraint naming** — `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_` prefixes, explicit names (auto-generated names are unstable and unreadable in error messages).
- **Optimistic concurrency on Recipes** — `RowVersion` token round-trip via `sp_GetRecipeFull` → `sp_UpdateRecipe`; raises 50004 if stale.
- **Categories = meal slots** — `MealPlanEntries.CategoryID` is a FK to `Categories`; the planner's weekly view renders 4 of the 6 categories as columns. UI choice, not a DB constraint.
- **Shopping list is computed**, not stored — `sp_GetShoppingList` joins planned meals through recipe ingredients with servings scaling, minus pantry on hand.
- **Pantry is unit-exact** — `(UserID, IngredientID, UnitID)` is the upsert key. No cross-unit conversion.
