---
tags: [database, schema]
---

# Schema Overview

Twelve tables in `MealPrepDB` (six core + two security/audit + three Phase 3 for meal planning + one Phase 4 for ingredient categorization). Build order is dependency order:

```
[[Users]]                  [[Units]]            [[Categories]]
   │                          │                      │
   │                          ▼                      │
   │                     [[Ingredients]]             │
   │                          │                      │
   ▼                          ▼                      ▼
[[Recipes]] ─────────► [[RecipeIngredients]] ◄───────┘
```

## Tables
- [[Users]] — accounts (Username, Email, PasswordHash) + security state (LastLoginAt, FailedLoginCount, LockedUntil)
- [[Units]] — measurement units (g, kg, ml, cup, …) with type (weight/volume/count)
- [[Categories]] — recipe categories (Breakfast, Lunch, …)
- [[Ingredients]] — global ingredient list with optional default unit; seeded with ~44 common items
- [[Recipes]] — owned by a user, optionally categorized; carries a `RowVersion` for optimistic concurrency
- [[RecipeIngredients]] — junction: recipe ↔ ingredient with quantity + unit
- **PasswordHistory** — recent password hashes per user (last 5 retained); cascade from Users
- **AuditLog** — append-only log of state-changing actions; written by every mutating proc
- [[MealPlanEntries]] — recipes scheduled to a date + meal slot (Category) per user
- [[RecipeFavorites]] — composite-PK join table for "user has favorited this recipe"
- [[UserPantry]] — current stock per user, per ingredient+unit (no cross-unit conversion in v1)
- [[IngredientCategories]] — Phase 4 lookup powering the Ingrediente sidebar; nullable FK from [[Ingredients]]

## API surface (Phase 2)
The .NET app does not query tables directly. It connects as the low-privilege `mealprep_app` SQL login and calls stored procs in `Database/procs/`:

| Area | Procs |
|---|---|
| Users / auth | `sp_RegisterUser`, `sp_GetUserForLogin`, `sp_GetUserProfile`, `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword` |
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
| Internal | `sp_WriteAudit` (called from mutating procs) |

The TVP type `dbo.IntList` is used by `sp_FindRecipesByIngredients` to accept an ingredient ID list.

## Security boundary
- `mealprep_app` SQL login is the app's principal.
- Member of `mealprep_app_role`:
  - `GRANT EXECUTE ON SCHEMA::dbo` (and `EXECUTE ON TYPE::dbo.IntList`)
  - `DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo`
- Mutations succeed only via stored procs through **SQL Server ownership chaining** (procs and tables both owned by `dbo`).
- `sa` is reserved for migrations only; the app never uses it.

## Cascade Behavior
- Delete a [[Recipes|Recipe]] → its [[RecipeIngredients]] rows are removed (`ON DELETE CASCADE`).
- Delete a [[Users|User]] → blocked if they own recipes (no cascade by design); but `PasswordHistory` rows DO cascade away (child has no meaning without parent).
- Delete an [[Ingredients|Ingredient]] → blocked if any recipe uses it.
- Delete a [[Units|Unit]] → blocked if any recipe ingredient uses it.

See [[Decisions Log]] for why cascades are intentionally minimal.

## Build Order
Run `Database/run_all.sql` end-to-end (idempotent) — the master script `:r`-includes everything in the right order. Manual order if needed:

**Phase 1 (schema + seeds)**
1. `00_create_database.sql`
2. `01_users.sql` … `06_recipe_ingredients.sql`
3. `seeds/units_seed.sql`, `seeds/categories_seed.sql`

**Phase 2 (security state, audit, API, login)**
4. `07_users_security.sql` — augments `Users`, creates `PasswordHistory`
5. `08_audit_log.sql` — `AuditLog` table + `IntList` TVP + `sp_WriteAudit`
6. `10_phase25_additions.sql` — Phase 2.5: FK index gaps + `RowVersion` on Recipes (must run before the procs that reference `RowVersion`)

**Phase 3 (meal planning, favorites, pantry)**
7. `11_meal_plan.sql` — `MealPlanEntries`
8. `12_favorites.sql` — `RecipeFavorites`
9. `13_pantry.sql` — `UserPantry`

**Phase 4 (ingredient categorization + reports)**
10. `14_ingredient_categories.sql` — `IngredientCategories` table + nullable FK column on `Ingredients`
11. `seeds/ingredient_categories_seed.sql` — 8 categories + backfill of shipped Ingredients

**Stored proc API (Phase 2 + 3 + 4)**
12. `procs/01_users.sql` … `procs/11_reports.sql` — full API. `sp_UpdateRecipe` requires `@RowVersion`; `sp_FindRecipesByIngredients` uses GROUP BY + LEFT JOIN to the TVP; `sp_AddPantryItem` is a MERGE upsert; `sp_GetShoppingList` is computed (no table); `sp_GetUserProfile` is the safe profile read (no PasswordHash exposed); `sp_GetIngredients` accepts optional `@IngredientCategoryID`.

**App login + role (run last)**
13. `09_app_role.sql` — login + role + grants. Requires `-v AppPassword="..."`.

**Error codes raised by procs**
- `50001` — password reused
- `50002` — not authorized
- `50003` — not found
- `50004` — stale row (optimistic concurrency conflict)
