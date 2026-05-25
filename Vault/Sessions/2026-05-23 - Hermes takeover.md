---
tags: [session, handoff, hermes]
date: 2026-05-23
---

# 2026-05-23 — Hermes takeover / project context absorbed

## Purpose
Codrin asked Hermes to take over from Claude as the working AI partner for the MealPrepApp practica project. This note records the context Hermes absorbed from the Obsidian vault and current repository state so future sessions have a clean handoff point.

## Current understanding
MealPrepApp is now a full WPF + SQL Server practica project, not just a database exercise. The repository contains:

- `App/MealPrepApp/` — Romanian WPF desktop client using MVVM, CommunityToolkit.Mvvm, Dapper, Microsoft.Data.SqlClient, DI, BCrypt, ClosedXML.
- `Database/` — SQL Server schema, seeds, stored-procedure-only API, app login/role, and idempotent `run_all.sql` build.
- `Vault/` — Obsidian source of truth for project history, decisions, TODOs, schema notes, and design spec.
- `Raport/` — Python-generated practica report (`Raport_practica.docx` / `.pdf`).

## Development history absorbed

### 2026-05-07 — Core database kickoff
- Built initial 6-table core: `Users`, `Units`, `Categories`, `Ingredients`, `Recipes`, `RecipeIngredients`.
- Added `run_all.sql`, unit/category seeds, Obsidian vault scaffold, per-table notes, and Decisions Log.
- Verified constraints, uniqueness, cascade `Recipes -> RecipeIngredients`, and RESTRICT behavior.

### 2026-05-07 — Phase 2 API/security
- Added `PasswordHistory`, `AuditLog`, user security columns, and `dbo.IntList` TVP.
- Added stored-proc API for auth, recipes, ingredients, lookups, and audit writing.
- Created low-privilege `mealprep_app` login/role: app can execute procs but is denied direct table DML/SELECT.
- Established important error codes: 50001 password reuse, 50002 unauthorized, 50003 not found, later 50004 stale row.

### 2026-05-11 — Phase 2.5 DB polish
- Seeded 44 common ingredients.
- Added missing FK indexes and `Recipes.RowVersion` optimistic concurrency.
- Rewrote `sp_FindRecipesByIngredients` using GROUP BY + LEFT JOIN to TVP.
- Added `sp_GetIngredientUsage`.
- Captured sqlcmd/docker rebuild pitfalls.

### 2026-05-11 — Phase 3 meal planning/pantry
- Added `MealPlanEntries`, `RecipeFavorites`, `UserPantry`.
- Added meal planning, favorite, pantry, shopping list, dashboard procs.
- Shopping list is computed, not stored; pantry is unit-exact; meal slot reuses `Categories`.

### 2026-05-11 — Phase 4 design/categories/reports
- Locked design direction: WPF + MVVM + Dapper, top tabs, Romanian v1, olive/cream/dark-brown palette.
- Added `IngredientCategories` and category backfill for seeded ingredients.
- Added profile-safe read and report procs.
- Design spec now defines screens for Auth, Acasa, Retete, Ingrediente, Planificare, Rapoarte, dialogs, empty/loading states.

### 2026-05-15 — Romanian data + AppPassword default
- Ingredients translated to Romanian without diacritics.
- `09_app_role.sql` now has empty `:setvar AppPassword ""` default for rebuild ergonomics.
- Clean rebuild verified.

### 2026-05-18 — App Phases A-F
- Phases A-E confirmed on Rita's PC: register/login/dashboard/recipes.
- Built Phase F: Ingrediente, Categorii grouping, Frigider, Lista de cumparaturi, add ingredient/pantry dialogs, Excel export, print.
- Added dialog service pattern for parameterless WPF dialogs with VM `DataContext`.

### 2026-05-21 — UI restyle and repo shift
- App restyled: custom ComboBox/SearchBox, live search, chrome-less windows, styled `MessageDialog`, DatePicker/Calendar/Menu/Tooltip/Scrollbar styles.
- `MessageBox.Show` replaced by `MessageDialog` via `IDialogService`.
- `App/` was committed; repo is no longer DB-only.

### 2026-05-22 — Recipe crash + Planificare/Rapoarte
- Recipe-save crash traced to duplicate ingredient (`Ulei`) causing SQL 2627. Fixed with UI duplicate guard + native SQL error mapping.
- Phase G Planificare shipped/merged: monthly and weekly planners, PlanMealDialog, `Adauga la plan` on recipe detail.
- Phase H Rapoarte shipped/merged: Statistici lunare, Plan saptamanal pentru tiparire, Lista cumparaturi pentru tiparire.
- GitHub repo moved from `Sklizm/MealPrepDB` to `Sklizm/MealPrepApp`.

### 2026-05-23 — Current repo state found by Hermes
- Current branch: `feature-drafts-and-photos`.
- `main` now includes merge of `fix-recipe-field-limits` (`Title MaxLength=150`, ingredient Notes 255), so the old TODO item about merging that branch appears stale.
- Work-in-progress files exist for Drafts and Photos:
  - `Database/15_recipe_drafts.sql`
  - `Database/16_recipe_photos.sql`
  - `Database/procs/12_recipe_drafts.sql`
  - `Database/procs/13_recipe_photos.sql`
  - `App/MealPrepApp/Models/RecipeDraft.cs`
  - `App/MealPrepApp/Data/Repositories/DraftRepository.cs`
  - `Database/run_all.sql` includes those scripts/procs.
- Draft design: incomplete recipe drafts in `RecipeDrafts`, ingredient rows stored as opaque JSON, owner-only CRUD procs.
- Photo design: one optional photo per recipe in `RecipePhotos`, stored as `VARBINARY(MAX)` after app downscales/re-encodes to JPEG, owner-only set/delete, public read by recipe ID.

## Conventions Hermes must preserve
- Trust SQL over notes if they disagree; update notes afterward.
- Use stored procedures as the app's only DB API. Do not add direct table access from the app.
- App connection is `mealprep_app`, never `sa`.
- Keep scripts idempotent: `IF OBJECT_ID...`, `CREATE OR ALTER`, `MERGE` seeds.
- Strings are `NVARCHAR`; timestamps are UTC via `SYSUTCDATETIME()`.
- Explicit constraint/index names: `PK_`, `FK_`, `UQ_`, `CK_`, `DF_`, `IX_`.
- FK columns need explicit indexes unless already covered as a leading column.
- `Recipes.RowVersion` is the optimistic concurrency token; stale recipe save is error 50004.
- User-facing strings are Romanian v1.
- Ingredient seed data is Romanian without diacritics.
- No price/nutrition/manual-shopping/unit-conversion unless schema/procs are added first.
- UI patterns: MVVM, DI-registered VMs, `IDialogService`, styled `MessageDialog`, chrome-less WPF windows, implicit global styles where app-wide theming is intended.

## Immediate likely next work
1. Finish the current `feature-drafts-and-photos` work by wiring drafts/photos into ViewModels and views.
2. Update stale TODOs: `fix-recipe-field-limits` appears already merged; origin URL may still need checking/updating.
3. Verify DB build with the new draft/photo scripts.
4. Verify app build/run on Windows/VM after draft/photo wiring.
5. Package as `.exe` and polish for final college delivery.

## Resume protocol for Hermes
At the start of future sessions on this repo:
1. Read `CLAUDE.md` and this takeover note.
2. Read `Vault/TODO.md` and latest `Vault/Sessions/*.md`.
3. Check `git status --short --branch` before editing.
4. If changing schema/procs, update `Database/run_all.sql`, relevant `Vault/Database/*.md`, `Vault/Decisions Log.md` if a non-trivial decision was made, and add a session note.
