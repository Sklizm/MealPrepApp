---
tags: [session]
---

# 2026-05-11 — Phase 3: Meal planning, favorites, pantry, shopping list

## Trigger

Codrin shared `~/Downloads/MealPrepApp.pdf` — the app design done with his gf (Railean Margarita). It revealed several features that v1 DB didn't support, most importantly the namesake feature: **meal planning**. We chose **Full** scope: planner + pantry + computed shopping list, plus a Favorites table. Hard delete kept. Categories table untouched. Out of scope: `IsArchived`, `PricePerServing`, `ImagePath`, dish-type categories, manual shopping-list additions, unit conversion.

## What was added

### Tables (3)
- `dbo.MealPlanEntries` — `(MealPlanEntryID, UserID, RecipeID, CategoryID, PlannedDate DATE, Servings NULL, Notes NULL, CreatedAt)`. `RecipeID` FK cascades on recipe delete; `UserID` and `CategoryID` RESTRICT. Indexed on `(UserID, PlannedDate)` for week/month reads, plus FK column indexes on RecipeID and CategoryID.
- `dbo.RecipeFavorites` — composite PK `(UserID, RecipeID)`. Both FKs cascade (no multi-path issue because Recipes→Users is RESTRICT).
- `dbo.UserPantry` — `(UserPantryID, UserID, IngredientID, UnitID, Quantity, AddedAt, UpdatedAt)` with `UQ (UserID, IngredientID, UnitID)`. `sp_AddPantryItem` MERGEs against the UQ so adding the same ingredient+unit again bumps quantity instead of duplicating rows.

### Procs (14 new)
- Meal plan: `sp_PlanMeal`, `sp_UpdatePlannedMeal`, `sp_UnplanMeal`, `sp_GetWeeklyPlan`, `sp_GetMonthlyPlan`
- Favorites: `sp_ToggleFavorite` (returns IsFavorite 0/1), `sp_GetFavoriteRecipes` (paged, same shape as `sp_GetRecipes`)
- Pantry: `sp_AddPantryItem` (upsert via MERGE), `sp_UpdatePantryQuantity` (absolute set), `sp_RemovePantryItem`, `sp_GetPantry`
- Shopping list: `sp_GetShoppingList(@UserID, @StartDate, @EndDate)` — computed, joins MealPlanEntries → Recipes → RecipeIngredients with servings scaling, LEFT JOIN UserPantry, returns rows where `NeededQty - OnHandQty > 0`.
- Dashboard: `sp_GetDashboardCounts` (Acasa 4-tile counts), `sp_GetRecentRecipes` (Retete Recente grid).

Total proc count: 32 (was 19 after Phase 2.5).

### Decisions baked in

- **Meal-slot = Category FK**: `MealPlanEntries.CategoryID` references `Categories`. The DB allows any of the 6 categories as a meal slot; the weekly UI just renders 4 columns (Breakfast/Lunch/Dinner/Snack). Decoupling DB from UI presentation lets a "Desert" entry exist without schema gymnastics.
- **Servings scaling in the shopping list**: `ri.Quantity * ISNULL(mpe.Servings, 1) / NULLIF(r.Servings, 0)`. If a 4-serving recipe is planned for 6, ingredient demand scales by 1.5×. `NULLIF` guards `Servings = 0`; `ISNULL` guards `Servings = NULL`. The math is verified end-to-end (planned 200g flour @ 6/4 = 300g; pantry had 50g; ToBuy = 250g — matched expected).
- **Pantry is unit-exact**: "500 g flour" and "2 cups flour" are separate rows. No conversion in v1.
- **Shopping list is computed, not stored**. No table; pure read proc. Manual ad-hoc items are v2.
- **Dashboard "MealsPlannedFromTodayCount"** uses `PlannedDate >= CAST(SYSUTCDATETIME() AS DATE)` so the tile reflects *upcoming* meals, which is what the user cares about on the home screen.

## Gotchas worth remembering

1. **`CASE` is not a legal parameter value for `EXEC`.** The first cut of `sp_ToggleFavorite` did `EXEC dbo.sp_WriteAudit @Details = CASE WHEN ... END;` and SQL Server rejected it (Msg 156). Fix: assign the CASE to a variable first, pass the variable. Same rule as the "no subqueries inside aggregates" gotcha — T-SQL is stricter about expressions in argument position than most languages.
2. **`INSERT…EXEC` doesn't play well with outer-transaction rollback** when the inner proc has its own BEGIN/COMMIT/CATCH-with-ROLLBACK. The CATCH inside the inner proc fires on FK violation and tries to ROLLBACK, which raises Msg 3915 inside the INSERT-EXEC scope. Workaround: don't wrap proc tests in an outer transaction; clean up explicitly with DELETE statements after.
3. **IDENTITY values keep climbing across rebuilds** since the schema uses `IF OBJECT_ID IS NULL` (table is not dropped, only created if missing). After a few rebuilds the seeded `Ingredients` IDs start at >2000. Tests should look up IDs by name, not hardcode.

## Verification (all green)

- 3 new tables present.
- 14 new procs present.
- Cascade verified: plan a recipe → delete it → plan entry vanishes.
- Favorites: toggle returns 1 then 0 across two calls.
- Pantry MERGE: 100 + 50 = 150 in single row.
- Shopping list end-to-end: 6/4 scaling correct; pantry subtraction correct; only `ToBuy > 0` rows surfaced.
- Weekly plan: 3 entries across a week return ordered by date + category.
- Dashboard: 4-column result set as designed.
- App login (`mealprep_app`) can EXEC every new proc; direct `SELECT FROM dbo.MealPlanEntries` is correctly denied.

## What's NOT in this phase

Per the user's choices:
- No `Recipes.IsArchived` — UI button stays as plain "Sterge".
- No `Recipes.PricePerServing` — `Pret/portie` column on the design's page-6 dashboard is dropped from v1.
- No `Recipes.ImagePath` — recipe cards stay text-only.
- No dish-type categories — Categories table keeps its current 6-row seed (Breakfast/Lunch/Dinner/Snack/Dessert/Drink).
- No manual shopping-list additions — list is fully computed.
- No unit conversion in shopping aggregation.
- No "recently viewed" tracking — `sp_GetRecentRecipes` uses `ISNULL(UpdatedAt, CreatedAt)`.

## Design feedback handed back to Codrin + gf

- Two nav models compete (top tabs vs tree nav). Pick one — top tabs (pages 1–5) is cleaner.
- Detail/editor screens aren't drawn yet: recipe editor, recipe view, ingredient editor, "add meal to plan" modal, confirmation dialogs.
- Calendar cell budget (7×6 grid × up to 4 meal slots/day) needs a compact representation + click-to-expand.
- `Rapoarte` is in nav but has no design.
- `aaa` Canva artifact on pages 2/3 — cleanup.
- Empty / loading / error states aren't drawn.

## Next

Phase 4 — actual .NET app implementation (owned by Codrin + gf, WPF + MVVM). The DB is now feature-complete for v1.
