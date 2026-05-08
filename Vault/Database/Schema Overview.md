---
tags: [database, schema]
---

# Schema Overview

Eight tables in `MealPrepDB` (six core + two security/audit). Build order is dependency order:

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
- [[Ingredients]] — global ingredient list with optional default unit
- [[Recipes]] — owned by a user, optionally categorized
- [[RecipeIngredients]] — junction: recipe ↔ ingredient with quantity + unit
- **PasswordHistory** — recent password hashes per user (last 5 retained); cascade from Users
- **AuditLog** — append-only log of state-changing actions; written by every mutating proc

## API surface (Phase 2)
The .NET app does not query tables directly. It connects as the low-privilege `mealprep_app` SQL login and calls stored procs in `Database/procs/`:

| Area | Procs |
|---|---|
| Users / auth | `sp_RegisterUser`, `sp_GetUserForLogin`, `sp_RecordLoginSuccess`, `sp_RecordLoginFailure`, `sp_ChangePassword` |
| Recipes (write) | `sp_CreateRecipe`, `sp_UpdateRecipe`, `sp_DeleteRecipe` |
| Recipes (read) | `sp_GetRecipeFull`, `sp_GetRecipes`, `sp_SearchRecipesByTitle`, `sp_FindRecipesByIngredients` |
| Ingredients | `sp_AddIngredient`, `sp_GetIngredients`, `sp_SearchIngredients` |
| Lookups | `sp_GetUnits`, `sp_GetCategories` |
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
6. `procs/01_users.sql` … `procs/05_lookups.sql` — the stored proc API
7. `09_app_role.sql` — login + role + grants (run last; depends on procs and TVP existing). Requires `-v AppPassword="..."`.
