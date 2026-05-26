---
tags: [session, nutrition, sql, wpf, verification]
---

# 2026-05-26 — Nutrition foundation

## Context
Codrin resumed work from the app recommendations / TODO list and chose to implement nutrition tracking next.

## Implemented
- Added `dbo.UnitConversions` for direct compatible unit conversions:
  - g ↔ kg
  - ml ↔ l
- Added `dbo.IngredientNutrition`, keyed one-to-one by `IngredientID`, with basis quantity/unit and calories/protein/carbs/fat values.
- Added nutrition stored procedures:
  - `sp_GetIngredientNutrition`
  - `sp_SetIngredientNutrition`
  - `sp_DeleteIngredientNutrition`
  - `sp_GetRecipeNutrition`
- Wired nutrition scripts into `Database/run_all.sql`.
- Added app models in `Models/Nutrition.cs`.
- Added `NutritionRepository` so the app stays stored-procedure-only.
- Added `IngredientNutritionDialog` and `IngredientNutritionDialogViewModel`.
- Added a `Nutritie` action to the Ingrediente list.
- Added a recipe detail `Nutritie estimata` card with total and per-serving values plus warnings for missing/unconvertible ingredients.
- Added/extended static regression coverage in `.hermes/tests/test_drafts_static.py`.
- Added `seeds/ingredient_nutrition_seed.sql` with demo nutrition defaults for the common seeded ingredients. The seed is insert-only, so manually corrected nutrition rows are preserved on rebuild.

## Verification performed locally
- Re-copied `Database/` into Docker container `MealPrepDB` and ran `Database/run_all.sql`; exit code 0.
- Ran a rolled-back SQL smoke test:
  - confirmed `UnitConversions` and `IngredientNutrition` tables exist;
  - confirmed all 4 nutrition procs exist;
  - confirmed conversion seed count is 10;
  - called `sp_SetIngredientNutrition` and `sp_GetIngredientNutrition` for a sample ingredient;
  - called `sp_GetRecipeNutrition` for a sample recipe;
  - rolled back the sample nutrition data.
- Ran `.hermes/tests/test_drafts_static.py` directly with Python; all static checks passed.
- XML-parsed changed nutrition XAML files:
  - `IngredientNutritionDialog.xaml`
  - `IngredienteListView.xaml`
  - `ReteteDetailView.xaml`
- Re-ran `Database/run_all.sql` after adding the nutrition seed; exit code 0 and inserted 44 nutrition seed rows.
- Verified sample seeded values for `Piept de pui`, `Orez`, `Cartof`, `Ou`, `Lapte`, and `Ulei de masline`.
- Verified the seed preserves manually edited rows by changing `Piept de pui` calories inside a transaction, re-running the seed, confirming the edited value remained, then rolling back.
- Attempted `dotnet build App/MealPrepApp/MealPrepApp.csproj --no-restore` on Fedora; it still cannot build locally because the Linux SDK lacks `Microsoft.NET.Sdk.WindowsDesktop` targets.

## Verification gap
- Closed: Codrin verified the nutrition UI on Rita's Windows machine/VM and confirmed it works.
- Follow-up UX note: entering every nutrition value manually is tedious, so a seed/import/preset improvement is worth considering.

## Next
- Continue with `.exe` conversion.
- Optional later: add CSV/import or external lookup if the built-in demo nutrition seed is not enough.
