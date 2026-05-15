---
tags: [todo]
---

# TODO

Running checklist. Check things off as they're done. Latest items at the top of each section.

## Now
- [ ] Margarita: revise the Canva mockup per Phase 4 design spec (11 new screens, 10 cleanup items, 4 cross-cutting components — see `Vault/Sessions/2026-05-11 - Phase 4 design and ingredient categories.md`)
- [ ] Refresh DataGrip and confirm the 12 tables + 38 procs + `IntList` type all show up
- [ ] Wire the .NET app to connect as `mealprep_app` (not `sa`) — connection string goes in `App/`'s config (WPF + MVVM + Dapper)

## Soon
- [ ] Optional: add convenience views for ad-hoc DataGrip exploration (read-only, separate role grants if exposed to the app)
- [ ] Optional: add an admin role for migrations distinct from `sa`
- [ ] Optional: `sp_GetAuditForUser` if the app wants a "your activity" feed

## Maybe Later (out of v1 scope, see [[Decisions Log]])
- [ ] Meal plans / weekly schedule tables
- [ ] Shopping list generation
- [ ] Nutrition tracking (calories, macros per ingredient)
- [ ] Recipe ratings / favorites
- [ ] Photos / image attachments
- [ ] User-private ingredients (add nullable UserID to [[Ingredients]])

## Done
- [x] Ingredients seed translated to Romanian (no diacritics, matching the IngredientCategories convention); ingredient-category backfill JOIN updated; `AppPassword` defaults to empty in `09_app_role.sql` so rebuilds no longer need `-v AppPassword=...`. Clean rebuild verified — see [[Sessions/2026-05-15 - Ingredients Romanian + AppPassword default]]
- [x] Phase 4 — Design completion + DB additions: locked auth/nav/click/calendar/categorii/rapoarte/iesire/plan-shortcut decisions; added `IngredientCategories` table + FK + 8-category seed + backfill of 44 ingredients; added `sp_GetUserProfile` (safe read), `sp_GetIngredientCategories`, `sp_GetMonthlyStats`, `sp_GetTopRecipes`, `sp_GetTopIngredients`; extended `sp_GetIngredients` with optional category filter
- [x] Phase 3 — Meal planning + pantry + shopping list: `MealPlanEntries`, `RecipeFavorites`, `UserPantry`, 14 new procs (plan/unplan/weekly/monthly, favorites toggle, pantry MERGE upsert, computed shopping list with servings scaling, dashboard counts + recents)
- [x] Phase 2.5 — DB polish: ingredients seeded (~44), FK index gaps closed, `RowVersion` on Recipes for optimistic concurrency (THROW 50004), `sp_FindRecipesByIngredients` rewritten as single GROUP BY + LEFT JOIN to TVP, new `sp_GetIngredientUsage`
- [x] Phase 2 — App API + security layer: 18 stored procs, `mealprep_app` low-priv login, `DENY` on direct DML, audit log, password history, lockout (5/15min)
- [x] End-to-end `run_all.sql` runs clean and idempotent
- [x] Verified app login can EXEC procs but cannot SELECT/INSERT/UPDATE/DELETE tables directly (ownership chaining covers mutations)
- [x] Ran `run_all.sql` against the Docker container via `sqlcmd` — all 6 tables created, 12 units + 6 categories seeded
- [x] Verified CHECK constraints fire (Quantity > 0, PrepTime/CookTime >= 0, Servings > 0, UnitType in allowed set)
- [x] Verified UNIQUE constraints fire (Users.Username, RecipeIngredients(RecipeID, IngredientID))
- [x] Verified `ON DELETE CASCADE` on Recipes → RecipeIngredients works
- [x] Verified RESTRICT on Ingredients delete (blocks if in use)
- [x] Project folder structure
- [x] `00_create_database.sql` — `MealPrepDB`
- [x] `01_users.sql` — [[Users]]
- [x] `02_units.sql` — [[Units]]
- [x] `03_categories.sql` — [[Categories]]
- [x] `04_ingredients.sql` — [[Ingredients]]
- [x] `05_recipes.sql` — [[Recipes]]
- [x] `06_recipe_ingredients.sql` — [[RecipeIngredients]]
- [x] `seeds/units_seed.sql`, `seeds/categories_seed.sql`
- [x] `run_all.sql` master script
- [x] Obsidian vault scaffolded
