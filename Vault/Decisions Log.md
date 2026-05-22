---
tags: [decisions, adr]
---

# Decisions Log

Architectural decisions with reasoning. Append, don't rewrite — even when reversed,
keep the original entry and add a follow-up entry that supersedes it.

---

## 2026-05-07 — Core scope only for v1
**Decision**: Ship with 6 tables (Users, Units, Categories, Ingredients, Recipes, RecipeIngredients). No meal plans, shopping lists, nutrition, or photos in v1.
**Why**: Practica needs a demoable result, not a complete product. Shipping a small thing that works beats shipping a big thing that doesn't.
**Trade-off**: We'll need a follow-up phase to add the rest. That's fine — the schema is designed to extend additively.

---

## 2026-05-07 — Ingredients are global (no UserID)
**Decision**: [[Ingredients]] table has no `UserID` — every user shares the same ingredient list.
**Why**: "Salt" doesn't need to be re-created for each user. Simpler schema, simpler queries, and the .NET app's autocomplete is better with a shared list.
**Reversibility**: Add a nullable `UserID` column later (`NULL` = global, otherwise = user-private). Existing data stays valid.

---

## 2026-05-07 — Only one cascade delete
**Decision**: `ON DELETE CASCADE` only on Recipes → [[RecipeIngredients]]. Everything else is RESTRICT.
**Why**: Cascading deletes feel convenient until they silently destroy data. RecipeIngredients rows have no meaning without their recipe, so cascading there is safe. Deleting a user with recipes, or an ingredient that's in use, should be an explicit operation — not a side effect.

---

## 2026-05-07 — UTC timestamps via SYSUTCDATETIME()
**Decision**: All `CreatedAt` / `UpdatedAt` columns default to `SYSUTCDATETIME()`.
**Why**: The Docker container's local time is whatever the host happens to be, and the .NET app may serve users in different timezones. UTC is the only stable reference. The app converts to local time for display.

---

## 2026-05-07 — Idempotent scripts
**Decision**: Every CREATE wrapped in `IF OBJECT_ID(...) IS NULL` (or equivalent for indexes). Seeds use `MERGE`.
**Why**: Re-running the build during development must be safe. No "drop and recreate" — that destroys local test data. No manual "did I already run this?" tracking.

---

## 2026-05-07 — Constraint naming convention
**Decision**: `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_` prefixes; column-specific suffix.
**Why**: Auto-generated constraint names (`PK__Users__1788CC4C7DEB...`) are unstable across rebuilds and unreadable in error messages. Explicit names make migration scripts and error logs much easier to read.

---

## 2026-05-07 — NVARCHAR everywhere
**Decision**: All string columns use `NVARCHAR` (Unicode), not `VARCHAR`.
**Why**: Recipe names, ingredient names, and instructions might include accented characters, emoji, or non-Latin scripts. Storage cost is negligible compared to the cost of a future migration.

---

## 2026-05-07 — Stored-proc-only API for the app
**Decision**: The .NET app will NOT have direct table access. It connects as a low-privilege SQL login (`mealprep_app`) that only has `GRANT EXECUTE ON SCHEMA::dbo`, with explicit `DENY SELECT/INSERT/UPDATE/DELETE ON SCHEMA::dbo`. Mutations succeed via ownership chaining (procs and tables share the `dbo` owner).
**Why**: SQL injection becomes structurally impossible from the app side — there is no path for an attacker-controlled string to land in an ad-hoc query. Also forces a clean DB/app contract: the procs are the API.
**Trade-off**: Every new query needs a new proc. For v1 this is fine — list of procs is bounded. If the app needs ad-hoc reporting later, add a read-only role with `GRANT SELECT` on specific views, not on tables.

---

## 2026-05-07 — Hard delete kept (no soft-delete)
**Decision**: Deletes physically remove rows. No `IsDeleted` flag.
**Why**: Practica scope is small; recovery isn't a goal. Soft-delete adds complexity to every read query (every WHERE needs `AND IsDeleted = 0`).
**Reversibility**: Adding soft-delete later is non-trivial — every proc and view would need to filter. If we ever do, it's a Phase 3 decision.

---

## 2026-05-07 — Lockout policy: 5 failures → 15 minutes
**Decision**: `sp_RecordLoginFailure` increments `FailedLoginCount`; on the 5th failure it sets `LockedUntil = now + 15 min` and writes `ACCOUNT_LOCKED` to AuditLog. Successful login resets both.
**Why**: Industry-standard order of magnitude. Long enough to deter brute force, short enough that legitimate users aren't catastrophically locked out.
**Reversibility**: Both numbers are local constants in `sp_RecordLoginFailure`. Easy to tune.

---

## 2026-05-07 — Password history depth: 5
**Decision**: `sp_ChangePassword` rejects reuse of the current password OR the last 5 entries in `dbo.PasswordHistory`. Pruning keeps history at exactly 5 rows per user.
**Why**: Common compliance default. More than enough to prevent obvious cycling, not so many that users feel boxed in.
**Reversibility**: `@HistoryDepth` is a local constant in the proc.

---

## 2026-05-07 — JSON for write payloads, TVP for read filters
**Decision**: `sp_CreateRecipe` / `sp_UpdateRecipe` accept ingredients as `@IngredientsJson NVARCHAR(MAX)` parsed via `OPENJSON`. `sp_FindRecipesByIngredients` accepts a `dbo.IntList` TVP.
**Why**: From C# / EF Core / Dapper, serializing a list to JSON with `System.Text.Json` is a one-liner; building a `DataTable` for a TVP is more code. But for pure ID lists in *read* paths, the TVP is cleaner and SQL Server can optimize it as a real (possibly-indexed) table.
**Trade-off**: Two payload styles in one API. Documented in proc-level comments.

---

## 2026-05-07 — App login password supplied at run time, not stored in the file
**Decision**: `09_app_role.sql` uses sqlcmd variable substitution (`$(AppPassword)`). The actual value is passed via `sqlcmd -v AppPassword="..."`.
**Why**: The file is committed to git; the password should not be. Run-time injection keeps the secret out of source control without giving up idempotency.
**How to apply**: Anyone re-running the build needs the password (kept by Codrin). Rotation is `ALTER LOGIN mealprep_app WITH PASSWORD = 'new'`.

---

## 2026-05-11 — IngredientCategories augments (not replaces) the global Ingredients pool
**Decision**: Added `dbo.IngredientCategories` (8 seeded rows) and a nullable `Ingredients.IngredientCategoryID` FK. The original "Ingredients are global (no UserID)" decision still holds — every user shares one ingredient pool. The category is a *display grouping*, not a privacy boundary.
**Why**: The app's Ingrediente sidebar has a "Categorii" entry; without a real grouping it would be a UI lie. Categorization is also a defensible practica answer ("how would you let users browse 200 ingredients?").
**How to apply**: New ingredients can ship with `IngredientCategoryID = NULL` (uncategorized → falls under "Altele" if the UI groups by category and renders NULL as Altele). The seed file backfills sensible values for the 44 shipped ingredients. Adding a new category is one row in the seed + a new code path in the UI grouping.

---

## 2026-05-11 — `sp_GetUserProfile` is the safe profile read; `sp_GetUserForLogin` stays login-only
**Decision**: New proc `sp_GetUserProfile(@UserID)` returns `UserID, Username, Email, CreatedAt, LastLoginAt` — no `PasswordHash`, no `FailedLoginCount`, no `LockedUntil`. The Profile screen calls this; `sp_GetUserForLogin` remains reserved for the login flow.
**Why**: `sp_GetUserForLogin` was originally designed to return the data the login flow needs to verify the password (including the hash). Reusing it for the Profile screen would leak the hash into a screen that has no business carrying it. Two procs with single, clear purposes is cheaper than auditing every caller.
**How to apply**: Any new "show me this user" screen should call `sp_GetUserProfile`. Only the login flow should ever read `PasswordHash`.

---

## 2026-05-11 — Design specification lives outside the repo
**Decision**: The design spec (Part A of Phase 4's plan) is handed to Margarita for the Canva mockups. It's referenced from `Vault/Sessions/2026-05-11 - Phase 4 design and ingredient categories.md` and the plan file, but the actual mockups aren't checked into this repo.
**Why**: This repo is the DB half. App design owns its own artifact (Canva file). Keeping them separate avoids the repo becoming the source of truth for two different deliverables maintained by two different people.
**How to apply**: When Margarita revises the mockups, the SoT is whichever Canva file she last saved. The plan file's Part A is a snapshot of the design intent at the moment of decision, not a living spec.

---

## 2026-05-11 — Meal-slot is a FK to Categories, not a separate enum
**Decision**: `dbo.MealPlanEntries.CategoryID` is a regular FK to `dbo.Categories`. The DB accepts any of the 6 categories (Breakfast/Lunch/Dinner/Snack/Dessert/Drink) as a meal slot; the weekly UI just chooses to render 4 columns.
**Why**: Adding a separate `MealSlot NVARCHAR CHECK IN (...)` column would have created a parallel taxonomy that overlaps Categories by 4 names but excludes Dessert and Drink — confusing and not really useful. Reusing Categories keeps the schema simple and lets a "Dessert" plan entry coexist naturally.
**How to apply**: The UI is responsible for which categories show up as columns in the weekly view. The DB makes no such judgment. If the app later decides to surface a 5th column for Desserts, no schema change is needed.

---

## 2026-05-11 — Shopping list is computed, not stored
**Decision**: `sp_GetShoppingList(@UserID, @StartDate, @EndDate)` is a pure read proc — no `ShoppingList` or `ShoppingListItem` table. The proc joins `MealPlanEntries → Recipes → RecipeIngredients` for the date range, scales by serving count, LEFT JOINs `UserPantry`, and returns ingredient lines with `NeededQty`, `OnHandQty`, `ToBuyQty`.
**Why**: A stored shopping list goes stale the moment a plan entry or a pantry stock changes. Computing on demand means the list is always current and there's no sync logic to maintain. For an offline single-user desktop app the perf cost is irrelevant.
**Trade-off**: Manual ad-hoc additions ("buy toilet paper even though no recipe needs it") aren't possible. If they're ever needed, add a `ManualShoppingItems` table and UNION it in.
**How to apply**: Don't add caching or materialization for this without a measured reason.

---

## 2026-05-11 — Servings scaling in the shopping list
**Decision**: Ingredient demand is computed as `ri.Quantity * ISNULL(mpe.Servings, 1) / NULLIF(r.Servings, 0)`. Planning a 4-serving recipe for 6 servings scales every ingredient by 1.5×.
**Why**: A recipe-level Servings count is meaningless unless the planner can override it. Otherwise the shopping list always reflects one canonical batch size, which doesn't match real cooking.
**Edge cases**: `NULLIF(r.Servings, 0)` guards a recipe with `Servings = 0` or `NULL` — the row drops out of the result via the `> 0` filter at the end. Treating it as 1 via `ISNULL` on the planning side covers the common case of "use the recipe's default".

---

## 2026-05-11 — Pantry is unit-exact (no conversion in v1)
**Decision**: `UserPantry` has `UQ (UserID, IngredientID, UnitID)` and the shopping list joins on the exact tuple. "500 g flour" and "2 cups flour" are tracked as two separate rows and don't combine.
**Why**: Cross-unit conversion (weight ↔ volume) needs density tables and ingredient-specific math. Way out of v1 scope, and 95% of practical pantry use is unit-consistent anyway.
**Reversibility**: A v2 conversion layer could fold the UQ to `(UserID, IngredientID)` after introducing canonical units per ingredient + a conversion function. The schema doesn't lock us out.

---

## 2026-05-11 — Pantry add is an upsert (MERGE), not an insert
**Decision**: `sp_AddPantryItem` MERGEs against `(UserID, IngredientID, UnitID)`. If the row exists, `Quantity += @Quantity, UpdatedAt = SYSUTCDATETIME()`; else insert.
**Why**: Matches the user mental model — "I bought 500 g more flour" is a single action, not "find my flour row, read its quantity, add 500, write it back". Also avoids race conditions inside a single user (one MERGE is atomic; read-modify-write isn't).
**How to apply**: Use `sp_UpdatePantryQuantity` (absolute set) for "user edited the number in the UI"; use `sp_AddPantryItem` for "user added more stock".

---

## 2026-05-11 — Cascade rules for new tables
**Decision**:
- `MealPlanEntries.RecipeID` → `ON DELETE CASCADE` (a planned entry without a recipe is nonsense).
- `MealPlanEntries.UserID` → RESTRICT (consistent with Recipes — deleting a user with plan entries is an explicit operation).
- `MealPlanEntries.CategoryID` → RESTRICT (Categories are seeded, never deleted in practice).
- `RecipeFavorites.UserID` and `.RecipeID` → both CASCADE. Safe because `Recipes.UserID` is RESTRICT, so no multi-cascade-path error.
- `UserPantry.UserID` → CASCADE; `UserPantry.IngredientID` and `.UnitID` → RESTRICT.
**Why**: Same principle as Phase 1 — cascade only when the child has no meaning without the parent.

---

## 2026-05-11 — EXEC parameters must be variables, not expressions
**Decision**: When the audit details for a proc are computed via CASE, assign to a local variable first:
```sql
DECLARE @Details NVARCHAR(500) = CASE WHEN @x = 1 THEN N'on' ELSE N'off' END;
EXEC dbo.sp_WriteAudit ..., @Details = @Details;
```
**Why**: T-SQL rejects `EXEC ... @param = CASE WHEN ... END` with Msg 156. `EXEC` parameters accept constants, variables, NULL, DEFAULT — not arbitrary expressions.
**How to apply**: If you find yourself writing `@param = (somefunc(x))` or `@param = ISNULL(...)` in an EXEC, hoist it to a `DECLARE @v = expr; EXEC ... @param = @v;` pattern.

---

## 2026-05-11 — Ingredient seed shape (~40 common items, MERGE keyed on Name, unit by abbreviation)
**Decision**: `seeds/ingredients_seed.sql` seeds ~40 common ingredients. Idempotent via `MERGE` on `Name`. `DefaultUnitID` is resolved via `LEFT JOIN dbo.Units ON Abbreviation = ...` rather than hardcoded unit IDs.
**Why**: The list needs to demo well (empty dropdowns kill the app's first impression), be re-runnable without dup or churn, and survive any future renumbering of Units. Abbreviation lookup means the seed file reads like the actual data, not like a foreign-key puzzle. `LEFT JOIN` (not `JOIN`) means a missing/renamed unit leaves `DefaultUnitID` NULL rather than dropping the ingredient.
**How to apply**: Adding more ingredients = append to the `VALUES` block. Renaming a unit abbreviation in `units_seed.sql` requires updating the matching column in the ingredients seed.

---

## 2026-05-11 — Optimistic concurrency on Recipes via ROWVERSION
**Decision**: `dbo.Recipes` gets a `RowVersion ROWVERSION NOT NULL` column. `sp_GetRecipeFull` returns it; `sp_UpdateRecipe` requires it as `@RowVersion BINARY(8)` and `THROW 50004` if it doesn't match the current row.
**Why**: Even though v1 is essentially single-user, the .NET app would otherwise have no defence against a second tab's stale save clobbering the first tab's changes. `ROWVERSION` is auto-maintained by SQL Server (no app or trigger involvement), so the cost is one extra parameter on update and one extra column on read. Good practica material — it surfaces a real production concern with minimal complexity.
**How to apply**: Any new mutating proc on Recipes should accept and check `@RowVersion`. Error 50004 is now reserved for stale-row conflicts across the API.

---

## 2026-05-11 — sp_FindRecipesByIngredients rewrite: GROUP BY + LEFT JOIN to TVP
**Decision**: The two `CROSS APPLY` subqueries are replaced by one `GROUP BY r.RecipeID` over `JOIN dbo.RecipeIngredients ri` + `LEFT JOIN @IngredientIDs m ON m.ID = ri.IngredientID`. `MatchedIngredients = SUM(CASE WHEN m.ID IS NOT NULL THEN 1 ELSE 0 END)`, `TotalIngredients = COUNT(*)`.
**Why**: The original recomputed two aggregates per recipe row via CROSS APPLY — a single pass with GROUP BY is faster and clearer. The LEFT JOIN form is the only way to do this in T-SQL: SQL Server rejects subqueries inside aggregate functions (Msg 130, "Cannot perform an aggregate function on an expression containing an aggregate or a subquery"), so `SUM(CASE WHEN x IN (SELECT ...))` is not legal.
**How to apply**: Any "count matches against a TVP" pattern should LEFT JOIN the TVP, not subquery against it inside an aggregate. Output shape of the proc is unchanged — callers don't need to know.

---

## 2026-05-11 — FK columns must be explicitly indexed
**Decision**: Added `IX_Ingredients_DefaultUnitID` and `IX_RecipeIngredients_UnitID`. The other FK columns were already covered (either by `IX_*` indexes or as leading columns of `UQ_*` / composite indexes).
**Why**: SQL Server does NOT auto-create an index for an FK column (only for the referenced PK). Without an index, the RESTRICT check on `DELETE FROM dbo.Units WHERE UnitID = X` scans the referencing table. Cheap to add, and a defensible answer to "why these indexes?" if asked.
**How to apply**: Any future FK addition needs a matching `IX_<Table>_<Column>` unless the column is already the leading column of another index.

---

## 2026-05-11 — Rebuild step: clean container target dir before docker cp
**Decision**: The full rebuild sequence is now: `docker exec -u 0 MealPrepDB rm -rf /tmp/Database` → `docker cp Database MealPrepDB:/tmp/Database` → `sqlcmd ... -i run_all.sql`.
**Why**: `docker cp Database MealPrepDB:/tmp/Database` copies INTO the existing `/tmp/Database/` (producing `/tmp/Database/Database/...`) when the target already exists. Symptom: the build "succeeds" against the stale outer copy and no new objects appear. The `-u 0` is needed because the existing files are owned by uid 1000 inside the container.
**How to apply**: Always clean before copying. If a future build report looks suspiciously clean (no rows affected for new seeds, etc.), check `/tmp/Database/` layout first.

---

## 2026-05-07 — Password history pruning needs a deterministic tiebreak
**Decision**: Both the "is this in the last 5 hashes?" check and the pruning `ROW_NUMBER()` order by `ChangedAt DESC, PasswordHistoryID DESC`. Not just `ChangedAt DESC`.
**Why**: `ChangedAt` is `DATETIME2(0)` (whole seconds). Multiple password changes in the same second have identical timestamps, so ordering by timestamp alone is non-deterministic and pruning can delete the wrong row. `PasswordHistoryID` is `INT IDENTITY` so it always grows monotonically — perfect tiebreak.
**How to apply**: Any `ORDER BY <timestamp>` over rows that can be created in rapid succession needs an IDENTITY tiebreak. Worth remembering for any future "recent N" query.

---

## 2026-05-15 — Ingredient names live in Romanian in the seed, not English
**Decision**: `seeds/ingredients_seed.sql` ships ingredient names in Romanian, written without diacritics (`Faina`, `Branza`, `Smantana`) to match the convention already used in [[IngredientCategories]]. No parallel English seed.
**Why**: The app ships in Romanian; a Romanian DB demo means a Romanian seed. Keeping an English copy as a sibling seed would let the two drift, and the `MERGE` is keyed on `Name` so any seed-time language swap is non-trivial (existing rows don't rename — they'd just sit alongside new ones).
**Reversibility / how to apply**: An English-language build of the app should add a localization layer (resource files in the app, or a `Translations` table keyed on `IngredientID`) rather than fork the seed. The seed is the canonical Romanian copy.

---

## 2026-05-15 — `AppPassword` defaults to empty inside `09_app_role.sql` (supersedes the 2026-05-07 run-time variable decision for rebuilds)
**Decision**: `09_app_role.sql` now declares `:setvar AppPassword ""` near the top so sqlcmd's preprocessor doesn't error on undefined variable. The `CREATE LOGIN ... WITH PASSWORD = N'$(AppPassword)'` line only fires when the login is missing, so on rebuilds the empty default is unused.
**Why**: The 2026-05-07 decision ("App login password supplied at run time, not stored in the file") is still correct in spirit, but its operational consequence — every rebuild needs `-v AppPassword=...` even though the login already exists and the value is discarded — was friction with no payoff. The login persists at the server level across `DROP DATABASE MealPrepDB`, so the *create* path is essentially first-run-only.
**Gotcha**: A `:setvar` in a script overrides `-v` from the command line, so the documented first-run path (`sqlcmd ... -v AppPassword="..."`) now also requires either editing the `:setvar` line in place or deleting it. The header comment of `09_app_role.sql` spells both paths out.
**How to apply**: Rebuilds → just run `run_all.sql`, no flag. First-time bring-up on a fresh server → edit `:setvar AppPassword ""` to the chosen password (or delete the line and use `-v`).

---

## 2026-05-18 — `sp_AddIngredient` cannot set category; Ingredient add dialog drops the picker
**Decision**: The Ingredient add dialog (`IngredientAddDialog`) presents only Name + DefaultUnit. New ingredients land in the "Fara categorie" group until a future proc accepts an `IngredientCategoryID` parameter.
**Why**: `Ingredients` has an `IngredientCategoryID` column (added in `14_ingredient_categories.sql`), and `sp_GetIngredients` returns it, but `sp_AddIngredient` does not accept it — so the design spec's intended "category dropdown" can't actually persist a category. Showing a non-functional picker would be worse UX than dropping it.
**How to apply**: When polish work needs editable categories, extend `sp_AddIngredient` to take `@IngredientCategoryID INT = NULL` (and add a parallel `sp_UpdateIngredientCategory` if backfill of existing rows is needed). Then re-add the picker in `IngredientAddDialogViewModel`.

---

## 2026-05-18 — Dialog-opening pattern: parameterless ctor + `IDialogService.ShowDialog<TWindow>(vm)`
**Decision**: Modal dialogs added in Phase F (`IngredientAddDialog`, `PantryItemDialog`) have parameterless constructors and bind the VM via `DataContext`. `IDialogService.ShowDialog<TWindow>(viewModel)` is the only entry point — it instantiates the window, sets `DataContext = vm`, sets `Owner` to the active window, and calls `ShowDialog()`. Dialogs close themselves by raising a `SaveSucceeded` event from the VM, handled in code-behind to set `DialogResult = true`.
**Why**: The existing `ChangePasswordDialog` was DI-constructed (its ctor takes the VM) because the shell window code-behind owns the open flow. Phase F dialogs are opened from list VMs (`IngredienteListViewModel`, `FrigiderViewModel`), not the shell, so funneling through `IDialogService` keeps those VMs testable and avoids spreading `App.Services.GetRequiredService` calls. The VM is still DI-resolved by the caller (so dependencies inject); the Window itself doesn't need to know about DI.
**How to apply**: New modal dialogs in Phases G/H should follow the same pattern unless they host raw controls (e.g. `PasswordBox`) whose values can't be bound directly — those keep the VM-in-ctor flavor.

---

## 2026-05-18 — Background-session isolation disabled for this repo (`.claude/settings.json`)
**Decision**: `.claude/settings.json` sets `{"worktree": {"bgIsolation": "none"}}`. Background Claude Code sessions can now edit `App/MealPrepApp/` directly instead of being routed through a worktree.
**Why**: The harness's default `bgIsolation: "worktree"` creates an isolated git worktree before allowing edits. But `App/` is gitignored — worktrees only carry tracked files, so the worktree would start without any existing app code. Any bg edit would land in an empty shell with no surrounding context.
**How to apply**: If/when `App/` becomes git-tracked (e.g. when the WinForms placeholder is finally removed and the WPF source is committed), revert this setting so bg sessions get back their isolation guard. Until then it stays off.

---

## 2026-05-18 (later) — `sp_AddIngredient` extended with `@IngredientCategoryID`; dialog regains the category picker
**Decision**: `sp_AddIngredient` now takes a third optional parameter `@IngredientCategoryID INT = NULL` and inserts it into `dbo.Ingredients`. `IngredientRepository.AddIngredientAsync` passes it through; `IngredientAddDialog` re-adds the category dropdown.
**Why**: The earlier same-day decision to drop the picker was a workaround for a v1 proc limitation, not a design choice. Extending the proc is a single-column, non-breaking change (new parameter is optional, defaults to NULL) — strictly better than shipping a dialog that silently ignores category.
**Reversibility**: Supersedes the earlier 2026-05-18 entry. The proc change is idempotent (`CREATE OR ALTER`) and already applied to the running container; smoke test confirmed an insert with `@IngredientCategoryID = 8 (Altele)` lands correctly.
**How to apply**: If `sp_UpdateIngredient` is ever added (for editing existing ingredients), match this signature: `(@IngredientID, @Name, @DefaultUnitID, @IngredientCategoryID)`.

---

## 2026-05-21 — Chrome-less windows via `System.Windows.Shell.WindowChrome`
**Decision**: All five windows (`LoginWindow`, `ShellWindow`, `ChangePasswordDialog`, `IngredientAddDialog`, `PantryItemDialog`) set `WindowStyle="None"` and use a `<shell:WindowChrome>` block (`CaptionHeight="44"`) to suppress the native Windows title bar while keeping OS drag/snap/resize. The existing dark 44px header strip becomes the OS drag region; custom caption buttons inside it are marked `shell:WindowChrome.IsHitTestVisibleInChrome="True"` so they receive clicks. Dialogs get only a × close; `LoginWindow` gets ─ minimize + ×; `ShellWindow` gets ─ ▢/❐ maximize-restore + ×.
**Why**: The custom dark header was rendering *below* the native Windows title bar — a visible double-header. `WindowChrome` is built into `PresentationFramework` (no extra dependency) and is the standard WPF way to own the whole window surface without losing OS window management.
**Gotcha**: Anything clickable in the caption region is otherwise swallowed by the drag area — every caption button needs `IsHitTestVisibleInChrome="True"`. `ResizeBorderThickness` must be non-zero (6) on resizable windows or the edges won't drag-resize; dialogs keep it at 0.
**How to apply**: New windows follow the same block. Caption-button styles live in `Themes/Styles.xaml` (`WindowChromeButton` 46×44 with `#33FFFFFF` hover, `WindowCloseButton` with `DangerBrush` hover).

---

## 2026-05-21 — `MessageDialog` replaces `MessageBox`; `DialogService` delegates to it
**Decision**: A single styled `Views/Shared/MessageDialog.xaml` (+ `.cs`) replaces every `MessageBox.Show`. It has the same dark-header / content / Cream2-footer chrome as the other dialogs and a `MessageDialogKind` enum: **Info** (one OK), **Confirm** (Da/Nu, returns true on Da), **Error** (`DangerBrush` red header + ⚠ glyph, one OK). `DialogService.Confirm/ShowError/ShowInfo` are now one-line delegators; the `IDialogService` interface is unchanged so no callers changed.
**Why**: Raw `MessageBox` is unstyleable native Windows chrome — it broke the cream/olive palette the moment any confirm/error fired. Centralising on one dialog means error styling (red + ⚠) is consistent and future tweaks happen in one place.
**Gotcha**: Title/message are assigned via named controls (`HeaderText.Text`, `MessageBody.Text`) in the static `Show()` factory **after** `InitializeComponent`, NOT via bindings to CLR window properties — those CLR props evaluate after the object initializer runs, so bindings would render empty.
**How to apply**: Never call `MessageBox.Show` again — route through `IDialogService`. New variants extend the enum + `Configure()` switch.

---

## 2026-05-21 — Native chrome themed via global implicit (keyless) styles
**Decision**: `DatePicker`/`Calendar`/`CalendarItem`/`CalendarDayButton`/`CalendarButton`, `Menu`/`MenuItem`, `ToolTip`, and `ScrollBar` (+ thumb/repeat-button parts) are restyled in `Themes/Styles.xaml` as **implicit** styles (TargetType with no `x:Key`) so every instance app-wide inherits the palette automatically — no callsite edits. Both `ShoppingListView` date fields, the `ShellWindow` user menu, every tooltip, and every `ScrollViewer`/`DataGrid` scrollbar pick them up for free.
**Why**: These controls leaked default blue/gray Windows chrome. Implicit styles theme them everywhere at once and keep new screens consistent without per-control wiring.
**Gotcha**: `CalendarButton` (month/year picker) has **no** `IsSelected` — that's `CalendarDayButton` only; use `HasSelectedDays` for its highlight (XAML compile error otherwise). The default `CalendarItem` template must be fully overridden or the native Aero chrome still frames the day grid. `SaveFileDialog` and `PrintDialog` are OS-native and **cannot** be restyled — documented limitation, out of scope.
**How to apply**: Keep new controls keyless-styled where an app-wide look is wanted; use a keyed style only for one-off variants.

---

## 2026-05-21 — Background-session isolation re-enabled now that `App/` is tracked (reverts the 2026-05-18 opt-out)
**Decision**: `App/` is now committed to git (115 files; `appsettings.Local.json`, `bin`/`obj`, and `App/*.zip` stay ignored). With the app tracked, the 2026-05-18 reason for `bgIsolation: "none"` no longer holds — a worktree now carries the app source — so the setting is reverted to the harness default.
**Why**: That entry explicitly said to revert "if/when `App/` becomes git-tracked." It just did, so bg sessions get their isolation guard back.
**How to apply**: `.claude/` is gitignored, so this is a local-machine change only; mirror it on any other machine that had the opt-out.

---

## 2026-05-22 — Rapoarte scope = the 3 design-spec sub-tabs, backed only by real procs
**Decision**: The Rapoarte tab ships exactly three sub-tabs — **Statistici lunare**, **Plan saptamanal pentru tiparire**, **Lista cumparaturi pentru tiparire** — and nothing else. The WinForms prototype's report cards ("Pret mediu per portie", "Calorii medii per reteta", "Alerta stoc", "Reteta cu calorii minime") were **deliberately dropped**.
**Why**: Those cards have no data behind them — the schema stores no price or nutrition, and there is no stock-alert proc. Statistici lunare maps to `sp_GetMonthlyStats` / `sp_GetTopRecipes` / `sp_GetTopIngredients`; the two print sub-tabs reuse `sp_GetWeeklyPlan` and `sp_GetShoppingList`. Every Rapoarte view is backed by an existing proc; nothing is faked.
**How to apply**: If price/nutrition is ever wanted, it's a schema + proc change first (see the "Maybe Later" TODO for nutrition), then a new sub-tab — not a hard-coded card. The print sub-tabs reuse the FlowDocument print + ClosedXML export helpers from the Ingrediente shopping list.

---

## 2026-05-22 — Recipe-save crash was a duplicate ingredient; guard at the UI + map native SQL errors
**Decision**: The opaque "eroare neasteptata" on save was SQL **2627** from a duplicate ingredient row (`UQ_RecipeIngredients_Recipe_Ingr`). Fix is two-layer: a duplicate guard in `ReteteEditorViewModel.Save` that blocks two rows with the same ingredient before the DB call, plus `DbExceptionMapper` mapping native codes (2627/2601/547/515/2628/8152) to Romanian messages and `AppDbException` appending `(cod N)` for any unmapped code.
**Why**: A unique-key violation reaching the user as a generic message is undiagnosable. Catching it at the UI gives a clear "ingredientul apare de mai multe ori"; the `(cod N)` fallback means the *next* unexpected failure carries its SQL number instead of being opaque.
**How to apply**: Validate against known constraints in the ViewModel before the round-trip; never let a raw `SqlException` surface untranslated — route through `AppDbException`/`DbExceptionMapper`.
