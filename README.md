# MealPrep — recipe tracking & meal planning

MealPrep is a full meal-prep, recipe-tracking, meal-planning and nutrition desktop application built as a school practica project.

The repo contains both halves of the project:

- `App/MealPrepApp/` — the Windows desktop client, built with WPF, MVVM and .NET 10.
- `Database/` — the SQL Server database, seeds and stored-procedure API used by the app.
- `Vault/` — the Obsidian project memory: decisions, sessions, TODOs, schema notes and Romanian counterparts.
- `Raport/` — the generated practica report source and output files.

The important architectural idea is that the app never reads or writes tables directly. It connects as the low-privilege `mealprep_app` SQL login and can only execute stored procedures. SQL Server tables remain protected behind a proc-only API.

## Main features

### User and security features

- User registration and login.
- BCrypt password hashing in the WPF app.
- SQL-side login success/failure tracking.
- Account lockout: 5 failed attempts triggers a 15-minute lockout.
- Password change with password-history protection.
- Forgot-password/demo reset flow from the login window.
- Audit logging for mutating database operations.
- Profile-safe user read procedure that never returns password hashes.

### Recipe features

- Recipe list, detail screen and editor.
- Recipe fields: title, category, description, instructions, prep/cook time and servings.
- Recipe ingredients stored as structured child rows.
- Recipe create/update/delete through stored procedures.
- Optimistic concurrency with `Recipes.RowVersion` so stale edits are rejected.
- Search by title.
- Find recipes by a selected ingredient list.
- Duplicate-ingredient guard in the editor, backed by DB uniqueness constraints.
- Recipe drafts: save incomplete recipe editor state and continue it later.
- Recipe photos: one optional image per recipe, stored in SQL Server after app-side downscaling/re-encoding.

### Ingredient, pantry and shopping-list features

- Seeded Romanian ingredient list for demos.
- Ingredient categories and grouped ingredient browsing.
- Live ingredient search.
- Add ingredient dialog.
- Pantry/fridge (`Frigider`) tracking per user.
- Pantry upsert behavior: adding more of the same ingredient/unit increases the quantity.
- Computed shopping list for a date range.
- Shopping list subtracts pantry quantities and scales requirements by planned servings.
- Shopping list export to Excel through ClosedXML.
- Printable shopping-list view.

### Meal-planning and report features

- Monthly and weekly meal-planning screens.
- Add a recipe to the meal plan from the recipe detail screen.
- Plan, update and unplan meals through stored procedures.
- Favorites support.
- Dashboard cards and recent recipes.
- Reports area with:
  - monthly statistics;
  - top recipes;
  - top ingredients;
  - weekly meal plan for printing;
  - shopping list for printing/export.

### Nutrition features

- `UnitConversions` table for direct compatible conversions, currently including g/kg and ml/l.
- `IngredientNutrition` table with per-ingredient nutrition basis values.
- Ingredient nutrition edit dialog.
- Estimated recipe nutrition card on recipe details.
- Total and per-serving calories/protein/carbs/fat.
- Missing or unconvertible nutrition rows are counted and shown instead of guessed.
- Demo nutrition seed for common seeded ingredients; it inserts missing rows only and preserves manually edited values.

### UX and packaging features

- Romanian UI.
- Cream/olive/dark-brown visual design.
- Chrome-less WPF windows using `WindowChrome`.
- Styled app-native `MessageDialog` instead of raw `MessageBox`.
- Themed DatePicker, Calendar, Menu, ToolTip and ScrollBar controls.
- Standalone startup loading window after login, before the main shell appears.
- Windows x64 self-contained single-file publish profile.
- `App/publish-windows-exe.cmd` helper for producing `MealPrepApp.exe` on Windows.

## App details (`App/MealPrepApp/`)

Technology stack:

- WPF on `net10.0-windows`.
- MVVM with `CommunityToolkit.Mvvm`.
- Data access with `Dapper` and `Microsoft.Data.SqlClient`.
- Dependency injection with `Microsoft.Extensions.DependencyInjection`.
- Configuration from `appsettings.json` plus local secret config in gitignored `appsettings.Local.json`.
- Password hashing with `BCrypt.Net-Next`.
- Excel export with `ClosedXML`.

Runtime flow:

1. `App.xaml.cs` builds the DI container and registers repositories, services, view-models and windows.
2. `LoginWindow` handles registration/login/forgot-password and creates the shell only after successful authentication.
3. `StartupLoadingWindow` shows a short standalone loading step while `ShellWindow.InitializeBeforeShowAsync()` prepares the first dashboard view.
4. Top-level navigation switches between Acasa, Retete, Ingrediente, Planificare and Rapoarte through view-model commands.
5. Repositories are the only data-access boundary; view-models do not run ad-hoc SQL.

Key app implementation details:

- Recipe edits send an ingredient JSON payload to `sp_CreateRecipe` / `sp_UpdateRecipe`.
- `Recipes.RowVersion` is loaded and sent back on update so stale edits can be rejected cleanly.
- Drafts save incomplete editor state separately from real recipes and can later be opened back into the editor.
- Photos are selected through the UI, downscaled in WPF, re-encoded as JPEG quality 85 and saved through recipe-photo procedures.
- Nutrition editing happens on ingredients; recipe details call the DB to calculate totals/per-serving values.
- Printing uses WPF `FlowDocument`; shopping-list export uses ClosedXML.

Important app folders:

```text
App/MealPrepApp/
├── Converters/          WPF converters, including byte[] -> image source
├── Data/                connection factory and repositories
├── Models/              DTOs/models used by repositories and view-models
├── Services/            dialog/session/navigation helpers
├── Themes/              Colors.xaml and Styles.xaml design system
├── ViewModels/          MVVM state and commands
├── Views/               WPF windows, pages and dialogs
├── appsettings.json     committed base config
└── appsettings.Local.template.json  safe publish-time template
```

Runtime config rule: the real `appsettings.Local.json` contains the `mealprep_app` password and must not be committed or published. Use the template when preparing a Windows publish folder.

## Database details (`Database/`)

The database is SQL Server 2022 and is designed to be rebuilt safely through the idempotent `run_all.sql` script.

Current object summary from the SQL scripts:

- 16 tables:
  - core: `Users`, `Units`, `Categories`, `Ingredients`, `Recipes`, `RecipeIngredients`;
  - security/audit: `PasswordHistory`, `AuditLog`;
  - meal planning: `MealPlanEntries`, `RecipeFavorites`, `UserPantry`;
  - lookups/extensions: `IngredientCategories`, `RecipeDrafts`, `RecipePhotos`, `UnitConversions`, `IngredientNutrition`.
- 50 stored procedures in the scripts, including the internal `sp_WriteAudit` helper. The public app API is the stored-procedure layer used by the repositories.
- Seed data for units, recipe categories, ingredient categories, common Romanian ingredients and demo nutrition values.
- Explicit constraints and indexes with stable names.
- `NVARCHAR` for strings and UTC timestamps via `SYSUTCDATETIME()`.

Database design rules:

- Build scripts are idempotent: tables are guarded with `IF OBJECT_ID(...) IS NULL`, procs use `CREATE OR ALTER`, and seeds use `MERGE` or insert-only patterns.
- The app login is least-privilege: `mealprep_app_role` gets `GRANT EXECUTE` but direct table `SELECT/INSERT/UPDATE/DELETE` is denied.
- Mutating procs write audit rows through `sp_WriteAudit`.
- FK columns get explicit indexes unless already covered by a leading key.
- Deleting child rows is intentionally conservative: `RecipeIngredients`, recipe photos, plan entries/favorites/drafts/pantry cascade only where the child has no standalone meaning; important source rows such as users, ingredients and units are protected by RESTRICT-style relationships.
- Shopping lists and nutrition totals are computed on demand instead of stored as stale snapshots.

Important database files:

```text
Database/
├── 00_create_database.sql
├── 01_users.sql ... 06_recipe_ingredients.sql
├── 07_users_security.sql
├── 08_audit_log.sql
├── 09_app_role.sql
├── 10_phase25_additions.sql
├── 11_meal_plan.sql
├── 12_favorites.sql
├── 13_pantry.sql
├── 14_ingredient_categories.sql
├── 15_recipe_drafts.sql
├── 16_recipe_photos.sql
├── 17_unit_conversions.sql
├── 18_ingredient_nutrition.sql
├── procs/                 stored-procedure API
├── seeds/                 idempotent seed scripts
└── run_all.sql             master build script
```

### Stored-procedure areas

| Area | Examples |
|---|---|
| Users/auth | `sp_RegisterUser`, `sp_GetUserForLogin`, `sp_GetUserProfile`, `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword`, `sp_ResetForgottenPassword` |
| Recipes | `sp_CreateRecipe`, `sp_UpdateRecipe`, `sp_DeleteRecipe`, `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients` |
| Ingredients/lookups | `sp_AddIngredient`, `sp_GetIngredients`, `sp_SearchIngredients`, `sp_GetIngredientUsage`, `sp_GetUnits`, `sp_GetCategories`, `sp_GetIngredientCategories` |
| Meal planning | `sp_PlanMeal`, `sp_UpdatePlannedMeal`, `sp_UnplanMeal`, `sp_GetWeeklyPlan`, `sp_GetMonthlyPlan` |
| Favorites/pantry/shopping | `sp_ToggleFavorite`, `sp_GetFavoriteRecipes`, `sp_AddPantryItem`, `sp_UpdatePantryQuantity`, `sp_RemovePantryItem`, `sp_GetPantry`, `sp_GetShoppingList` |
| Reports/dashboard | `sp_GetDashboardCounts`, `sp_GetRecentRecipes`, `sp_GetMonthlyStats`, `sp_GetTopRecipes`, `sp_GetTopIngredients` |
| Drafts/photos | `sp_SaveDraft`, `sp_GetDrafts`, `sp_GetDraft`, `sp_DeleteDraft`, `sp_SetRecipePhoto`, `sp_GetRecipePhoto`, `sp_DeleteRecipePhoto` |
| Nutrition | `sp_GetIngredientNutrition`, `sp_SetIngredientNutrition`, `sp_DeleteIngredientNutrition`, `sp_GetRecipeNutrition` |
| Internal | `sp_WriteAudit` |

Custom SQL error codes used by the app:

- `50001` — password reused.
- `50002` — not authorized.
- `50003` — not found.
- `50004` — stale recipe row / optimistic concurrency conflict.
- `50005` — no matching account in the forgot-password reset flow.

## Running the database

Start a SQL Server 2022 container:

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=<your-sa-password>" \
  -p 1433:1433 --name MealPrepDB \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

Build or rebuild the schema. The script is idempotent and safe to re-run:

```bash
docker exec -u 0 MealPrepDB rm -rf /tmp/Database
docker cp Database MealPrepDB:/tmp/Database
docker exec -w /tmp/Database MealPrepDB /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$(docker exec MealPrepDB printenv SA_PASSWORD)" \
  -C -b -i run_all.sql
```

Notes:

- `mssql-tools18` requires `-C` because TLS is enforced by default.
- `-b` makes `sqlcmd` exit non-zero on SQL errors.
- `run_all.sql` uses `:r` includes, so run it with `sqlcmd`, not a generic SQL console unless that console supports sqlcmd mode.
- The `docker exec -u 0 ... rm -rf /tmp/Database` cleanup prevents `docker cp` from nesting a fresh copy inside an older copied directory.
- First-time creation of `mealprep_app` requires setting the password in `09_app_role.sql` or passing it through sqlcmd as documented in that script. Rebuilds usually do not need the password because the server-level login already exists.

Run a quick query:

```bash
docker exec MealPrepDB /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$(docker exec MealPrepDB printenv SA_PASSWORD)" \
  -C -d MealPrepDB -h-1 -W -Q "SELECT name FROM sys.tables ORDER BY name;"
```

## App connection string

The app connects as `mealprep_app`, never `sa`:

```text
Server=localhost,1433;Database=MealPrepDB;User Id=mealprep_app;Password=<app-login-password>;TrustServerCertificate=true;
```

For local development, place this in `App/MealPrepApp/appsettings.Local.json`. Do not commit that file.

## Running the app on Windows

Prerequisites:

- Windows machine/VM.
- .NET 10 SDK.
- SQL Server container/database running and reachable.
- `appsettings.Local.json` with the real app-login password.

From the repo root on Windows:

```cmd
cd App\MealPrepApp
dotnet run
```

Or open `App/MealPrepApp/MealPrepApp.csproj` in Visual Studio and run it from there.

Note: WPF WindowsDesktop projects cannot be built or run properly on this Fedora/Linux development environment because the Linux SDK does not include the WindowsDesktop targets. Runtime verification is done on a Windows machine/VM.

## Publishing a Windows `.exe`

On Windows with the .NET 10 SDK installed, run from the repo root:

```cmd
App\publish-windows-exe.cmd
```

Expected output:

```text
App\publish\MealPrepApp-win-x64\MealPrepApp.exe
```

The publish profile is Windows x64, self-contained and single-file. It deliberately does not publish the real `appsettings.Local.json`.

Before running the exe:

1. Copy `appsettings.Local.template.json` to `appsettings.Local.json` in the same folder as `MealPrepApp.exe`.
2. Replace `__SET_APP_PASSWORD__` with the real `mealprep_app` password.
3. Make sure SQL Server is reachable from that machine.

## Repo layout

```text
App/                  WPF desktop app and Windows publish helper
Database/             SQL Server schema, procs, seeds and run_all.sql
Raport/               Python report generator plus generated DOCX/PDF report
Vault/                Obsidian vault for project notes and history
CLAUDE.md             agent/project instructions
README.md             this file
```

## Obsidian vault

`Vault/` is the explicit cross-session source of truth for project history. It contains:

- project overview and tech stack;
- schema overview and per-table notes;
- architectural decisions in `Decisions Log.md`;
- TODO tracking;
- dated session notes;
- Romanian `-ro.md` counterparts for the notes used in the practica documentation.

When notes and SQL disagree, trust the SQL scripts and update the vault.

## Load-bearing design decisions

The full rationale lives in `Vault/Decisions Log.md`. The most important decisions are:

- Stored-procedure-only app API.
- Least-privilege `mealprep_app` login.
- Idempotent database scripts.
- Explicit constraint/index names.
- UTC timestamps.
- `NVARCHAR` strings.
- Recipes use optimistic concurrency with `RowVersion`.
- Shopping list is computed, not stored.
- Pantry quantities are unit-exact.
- Drafts store incomplete editor state separately from real recipes.
- Photos are stored in SQL Server, one optional photo per recipe.
- Nutrition is ingredient-sourced and calculated per recipe on demand.
