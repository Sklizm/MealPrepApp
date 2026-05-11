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
