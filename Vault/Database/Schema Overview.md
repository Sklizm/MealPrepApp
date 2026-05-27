---
tags: [database, schema]
---

# Schema Overview

Sixteen tables in `MealPrepDB` (six core + two security/audit + three meal-planning/pantry + one ingredient categorization lookup + drafts/photos + two nutrition tables). Build order is dependency order:

```text
[[Users]]                  [[Units]]            [[Categories]]
   │                          │                      │
   │                          ▼                      │
   │                     [[Ingredients]]             │
   │                       │      │                  │
   ▼                       │      ▼                  ▼
[[Recipes]] ─────────► [[RecipeIngredients]] ◄───────┘
   │     │                     │
   │     ▼                     ▼
   │  [[RecipePhotos]]    [[IngredientNutrition]]
   ▼
[[RecipeDrafts]]
```

## Tables
- [[Users]] — accounts (Username, Email, PasswordHash) + security state (LastLoginAt, FailedLoginCount, LockedUntil)
- [[Units]] — measurement units (g, kg, ml, cup, …) with type (weight/volume/count)
- [[Categories]] — recipe categories (Breakfast, Lunch, …), also reused as meal-plan slots
- [[Ingredients]] — global ingredient list with optional default unit and optional ingredient category; seeded with common Romanian items
- [[Recipes]] — owned by a user, optionally categorized; carries a `RowVersion` for optimistic concurrency
- [[RecipeIngredients]] — junction: recipe ↔ ingredient with quantity + unit
- **PasswordHistory** — recent password hashes per user (last 5 retained); cascades from Users
- **AuditLog** — append-only log of state-changing actions; written by mutating procs
- [[MealPlanEntries]] — recipes scheduled to a date + meal slot per user
- [[RecipeFavorites]] — composite-PK join table for "user has favorited this recipe"
- [[UserPantry]] — current stock per user, per ingredient+unit; unit-exact by design
- [[IngredientCategories]] — lookup powering the Ingrediente sidebar; nullable FK from [[Ingredients]]
- [[RecipeDrafts]] — partially-complete recipe editor saves per user; nullable fields + opaque ingredient JSON
- [[RecipePhotos]] — one optional DB-stored photo per recipe
- [[UnitConversions]] — direct compatible conversions used by nutrition calculations
- [[IngredientNutrition]] — nutrition source data per ingredient, used to calculate recipe totals on demand

## Stored-procedure API surface
The .NET app does not query tables directly. It connects as the low-privilege `mealprep_app` SQL login and calls stored procs in `Database/procs/`:

| Area | Procs |
|---|---|
| Users / auth | `sp_RegisterUser`, `sp_GetUserForLogin`, `sp_GetUserProfile`, `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword`, `sp_ResetForgottenPassword` |
| Recipes (write) | `sp_CreateRecipe`, `sp_UpdateRecipe`, `sp_DeleteRecipe` |
| Recipes (read) | `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients` |
| Ingredients | `sp_AddIngredient`, `sp_GetIngredients`, `sp_SearchIngredients`, `sp_GetIngredientUsage` |
| Lookups | `sp_GetUnits`, `sp_GetCategories`, `sp_GetIngredientCategories` |
| Meal plan | `sp_PlanMeal`, `sp_UpdatePlannedMeal`, `sp_UnplanMeal`, `sp_GetWeeklyPlan`, `sp_GetMonthlyPlan` |
| Favorites | `sp_ToggleFavorite`, `sp_GetFavoriteRecipes` |
| Pantry | `sp_AddPantryItem`, `sp_UpdatePantryQuantity`, `sp_RemovePantryItem`, `sp_GetPantry` |
| Shopping list | `sp_GetShoppingList` (computed, joins planned meals minus pantry) |
| Dashboard | `sp_GetDashboardCounts`, `sp_GetRecentRecipes` |
| Reports | `sp_GetMonthlyStats`, `sp_GetTopRecipes`, `sp_GetTopIngredients` |
| Drafts | `sp_SaveDraft`, `sp_GetDrafts`, `sp_GetDraft`, `sp_DeleteDraft` |
| Photos | `sp_SetRecipePhoto`, `sp_GetRecipePhoto`, `sp_DeleteRecipePhoto` |
| Nutrition | `sp_GetIngredientNutrition`, `sp_SetIngredientNutrition`, `sp_DeleteIngredientNutrition`, `sp_GetRecipeNutrition` |
| Internal | `sp_WriteAudit` (called from mutating procs) |

The TVP type `dbo.IntList` is used by `sp_FindRecipesByIngredients` to accept an ingredient ID list.

## Security boundary
- `mealprep_app` SQL login is the app's principal.
- Member of `mealprep_app_role`:
  - `GRANT EXECUTE ON SCHEMA::dbo` and `EXECUTE ON TYPE::dbo.IntList`
  - `DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo`
- Mutations succeed only via stored procs through SQL Server ownership chaining (procs and tables both owned by `dbo`).
- `sa` is reserved for migrations only; the app never uses it.

## Cascade behavior
- Delete a [[Recipes|Recipe]] -> its [[RecipeIngredients]] rows, [[RecipePhotos|RecipePhoto]] row, meal-plan entries and favorites are removed when those FK rules apply.
- Delete a [[Users|User]] -> blocked if they own recipes; `PasswordHistory`, [[RecipeDrafts]], [[RecipeFavorites]] and [[UserPantry]] rows cascade where designed.
- Delete an [[Ingredients|Ingredient]] -> blocked if recipes, pantry or nutrition depend on it.
- Delete a [[Units|Unit]] -> blocked if recipe ingredients, pantry, conversions or nutrition basis rows depend on it.

See [[Decisions Log]] for the rationale behind each cascade/restrict choice.

## Build order
Run `Database/run_all.sql` end-to-end (idempotent). Manual order if needed:

1. `00_create_database.sql`
2. `01_users.sql` … `06_recipe_ingredients.sql`
3. `seeds/units_seed.sql`, `seeds/categories_seed.sql`
4. `07_users_security.sql`, `08_audit_log.sql`, `10_phase25_additions.sql`
5. `11_meal_plan.sql`, `12_favorites.sql`, `13_pantry.sql`
6. `14_ingredient_categories.sql`, `seeds/ingredient_categories_seed.sql`, `seeds/ingredients_seed.sql`
7. `15_recipe_drafts.sql`, `16_recipe_photos.sql`
8. `17_unit_conversions.sql`, `18_ingredient_nutrition.sql`, `seeds/ingredient_nutrition_seed.sql`
9. `procs/01_users.sql` … `procs/14_nutrition.sql`
10. `09_app_role.sql` — run last for login/role/grants. Rebuilds usually do not need an app password because the login already exists; first-time setup follows the comments in that script.

## Error codes raised by procs
- `50001` — password reused
- `50002` — not authorized
- `50003` — not found
- `50004` — stale row / optimistic concurrency conflict
- `50005` — no matching account for forgot-password reset
