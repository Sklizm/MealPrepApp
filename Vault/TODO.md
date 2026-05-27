---
tags: [todo]
---

# TODO

Running checklist. Check things off as they're done. Latest items at the top of each section.

## Now
- [x] **Windows exe publish/runtime verification** — run `App\publish-windows-exe.cmd` on Rita's Windows machine/VM; confirm `MealPrepApp.exe` is created and launches with a valid `appsettings.Local.json`

## Soon
- [ ] ~~Optional: add convenience views for ad-hoc DataGrip exploration (read-only, separate role grants if exposed to the app)~~
- [ ] ~~Optional: add an admin role for migrations distinct from `sa`~~
- [ ] ~~Optional: `sp_GetAuditForUser` if the app wants a "your activity" feed
- [ ] If no photo on a recipe then default photo of a aesthetic knife

## Maybe Later (out of v1 scope, see [[Decisions Log]])
- [ ] User-private ingredients (add nullable UserID to [[Ingredients]])

## Done
- [x] **README and Romanian vault counterparts updated** — root `README.md` now documents DB/app/features in more detail; missing `-ro` counterparts were added for database/session notes; schema overview and index notes were refreshed for drafts, photos, unit conversions and nutrition
- [x] **Practice report regenerated and requirement-checked** — `Raport/Raport_practica.docx` and `.pdf` were regenerated from the Python generator; report now reflects the final completed app features (forgot password, drafts, photos, loading, nutrition, Windows exe publishing) and includes Anexa A5 requirement checklist
- [x] **Windows exe publish path implemented** — added a Windows x64 self-contained single-file publish profile, `App\publish-windows-exe.cmd`, a safe `appsettings.Local.template.json`, and README instructions; local static checks pass, actual WPF publish needs Windows verification
- [x] **Common nutrition seed added** — `seeds/ingredient_nutrition_seed.sql` now inserts demo nutrition defaults for the 44 common seeded ingredients; it is wired into `run_all.sql`, verified in Docker, and preserves manually edited nutrition rows on rebuild
- [x] **Nutrition feature Windows verification passed on Rita's machine/VM** — Codrin confirmed ingredient nutrition edit/save and recipe-detail nutrition display work; manual entry is usable but tedious, so seeding/import/presets are a possible follow-up
- [x] **Nutrition tracking foundation implemented** — added `UnitConversions`, `IngredientNutrition`, nutrition stored procs, ingredient nutrition editor dialog, Ingrediente `Nutritie` action, and recipe detail `Nutritie estimata` totals; SQL/static/XAML checks pass locally, pending Rita Windows runtime verification
- [x] **Forgot-password reset flow verified on Rita's Windows machine/VM** — Codrin confirmed the login-window reset flow works properly end-to-end
- [x] **Forgot-password reset flow implemented** — login window now has `Ai uitat parola?`, opens `ForgotPasswordDialog`, and resets the password through `sp_ResetForgottenPassword`; SQL/static/XAML checks pass locally and Rita Windows runtime verification passed
- [x] **Missing-ingredient-from-recipe-editor task dismissed** — Codrin chose to skip this item and prioritize the forgot-password/change-password flow instead
- [x] **Standalone loading window and Photos UI verified on Rita's machine** — Codrin confirmed the Windows target-machine check passed and everything works properly
- [x] **Standalone loading window before shell implemented** — after login the app now shows a separate `StartupLoadingWindow`, keeps the shell hidden, initializes Acasa in the background, enforces a 3.5 second minimum display time, and only then shows the main app; verified on Rita's machine
- [x] **Loading screen after login implemented** — superseded by the standalone pre-shell loading window; original version used a smooth modal startup overlay while Acasa/dashboard data loaded
- [x] **Photos initial UI wiring implemented** — detail screen can load, add/change, and delete a recipe photo via `sp_GetRecipePhoto`/`sp_SetRecipePhoto`/`sp_DeleteRecipePhoto`; verified on Rita's machine
- [x] **Drafts UI wiring verified by Codrin** — add/open/delete draft flow works properly; wording switched from ciorna/ciorne to Drafts / `Salveaza ca draft`
- [x] **Drafts initial UI wiring implemented** — registered `DraftRepository`, added Retete > Ciorne list/open/delete flow, added editor `Salveaza ciorna` save/load flow; pending Windows/.NET 10 verification
- [x] **Draft/photo database scripts verified through `run_all.sql`** — full SQL Server build exited 0; verified `RecipeDrafts`, `RecipePhotos`, and draft/photo procs exist; `mealprep_app_role` still has EXECUTE grant plus direct DML denies
- [x] **Origin URL updated** — `origin` now points to `https://github.com/Sklizm/MealPrepApp.git`
- [x] **`fix-recipe-field-limits` merged** — git history contains merge `25276e3 Merge branch 'fix-recipe-field-limits'`
- [x] **Meal planning / weekly schedule shipped** — covered by Phase G Planificare
- [x] **Shopping list generation shipped** — covered by Phase F/H computed shopping list + print/export flows
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
