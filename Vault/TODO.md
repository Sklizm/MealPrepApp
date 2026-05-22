---
tags: [todo]
---

# TODO

Running checklist. Check things off as they're done. Latest items at the top of each section.

## Now
- [ ] Merge `fix-recipe-field-limits` (Title `MaxLength=150`, ingredient Notes `255`) after VM verification
- [ ] Update `origin` URL — GitHub repo moved `Sklizm/MealPrepDB` → `Sklizm/MealPrepApp`

## Soon
- [ ] Optional: add convenience views for ad-hoc DataGrip exploration (read-only, separate role grants if exposed to the app)
- [ ] Optional: add an admin role for migrations distinct from `sa`
- [ ] Optional: `sp_GetAuditForUser` if the app wants a "your activity" feed
- [ ] Loading screen on app launch
- [ ] Conversion to .exe
- [ ] Add Drafts
- [ ] Add the ability of adding an ingredient when making a recipe if said ingredient does not currently exist in the DB
- [ ] Add a function to change password in login window in case forgot
- [ ] Ability to add photos to recipes

## Maybe Later (out of v1 scope, see [[Decisions Log]])
- [ ] Meal plans / weekly schedule tables
- [ ] Shx`opping list generation
- [ ] Nutrition tracking (calories, macros per ingredient)
- [x] Recipe ratings / favorites
- [ ] Photos / image attachments
- [ ] User-private ingredients (add nullable UserID to [[Ingredients]])

## Done
- [x] **Phase H (Rapoarte) confirmed on the VM + merged** — `RapoarteRootViewModel` with 3 sub-tabs: Statistici lunare (KPI tiles + per-slot + top recipes/ingredients), Plan saptamanal pentru tiparire, Lista cumparaturi pentru tiparire (print + Excel). See [[Sessions/2026-05-22 - Recipe crash fixed; Planificare + Rapoarte shipped]]
- [x] **Phase G (Planificare) confirmed on the VM + merged** — Lunar/Saptamanal calendar + Plan-meal dialog + "Adauga la plan" on recipe detail, on the existing `MealPlanEntries` procs
- [x] **Recipe-save crash root cause = duplicate ingredient ("Ulei" twice → SQL 2627)** — fixed with an editor duplicate guard + native-error mapping (`DbExceptionMapper`/`AppDbException`, `(cod N)` fallback)
- [x] **UI restyle confirmed on Rita's PC + a Windows 11 VM** — dropdowns/live-search, chrome-less windows (`WindowChrome`), styled `MessageDialog` (Info/Confirm/Error), themed DatePicker/Calendar/Menu/ToolTip/ScrollBar via global implicit styles — see [[Sessions/2026-05-21 - UI restyle: dropdowns, popups, window chrome]]
- [x] **`App/` committed to git** — project is no longer DB-only; root `.gitignore` + `CLAUDE.md` updated; `appsettings.Local.json`/`bin`/`obj`/`App/*.zip` stay out; `bgIsolation` reverted to default
- [x] Phase F (Ingrediente / Frigider / Lista cumparaturi) confirmed working on Rita's PC
- [x] Margarita: revise the Canva mockup per Phase 4 design spec
- [x] Refresh DataGrip and confirm the 12 tables + 38 procs + `IntList` type all show up
- [x] Phases A–E of the WPF app (Skeleton/Infra, Models/Data, Auth, Shell/Acasa, Retete) confirmed working on Rita's PC — see [[Sessions/2026-05-18 - Phases A-E confirmed, Phase F built]]
- [x] Wired the .NET app to connect as `mealprep_app` (not `sa`); connection string lives in `appsettings.Local.json` (gitignored)
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
