---
tags: [database, table, nutrition]
---

# IngredientNutrition

File: `Database/18_ingredient_nutrition.sql`

## Purpose
Stores nutrition facts at ingredient level so recipe nutrition can be calculated on demand.

Nutrition is intentionally not stored as calculated totals on `Recipes`, because those totals would go stale when ingredients, quantities, or nutrition values change.

## Columns
| Column | Type | Notes |
|---|---|---|
| IngredientID | INT | PK and FK -> [[Ingredients]]; one nutrition row per ingredient |
| BasisQuantity | DECIMAL(10,2) | quantity the nutrition values refer to, usually 100 |
| BasisUnitID | INT | FK -> [[Units]]; basis unit such as g, ml, or pc |
| Calories | DECIMAL(10,2) | kcal for the basis quantity |
| ProteinGrams | DECIMAL(10,2) | protein for the basis quantity |
| CarbsGrams | DECIMAL(10,2) | carbohydrates for the basis quantity |
| FatGrams | DECIMAL(10,2) | fat for the basis quantity |
| UpdatedAt | DATETIME2(0) | UTC default; updated on changes |

## Stored procedures
- `sp_GetIngredientNutrition` — reads nutrition for one ingredient.
- `sp_SetIngredientNutrition` — inserts or updates nutrition for one ingredient.
- `sp_DeleteIngredientNutrition` — removes nutrition for one ingredient.
- `sp_GetRecipeNutrition` — calculates total/per-serving calories, protein, carbs and fat for a recipe.

## Seed data
`seeds/ingredient_nutrition_seed.sql` inserts demo nutrition defaults for the common seeded ingredients. It only inserts missing rows, so manually corrected app values are preserved during rebuilds.

## Design notes
- Recipe nutrition is calculated through stored procedures, not direct table reads.
- `UnitConversions` provides only direct compatible conversions; missing or incompatible rows are counted and displayed as incomplete.
- Future daily/weekly nutrition reports should build on this ingredient-sourced model.

See [[UnitConversions]], [[Ingredients]], [[Recipes]], [[Schema Overview]], [[Decisions Log]]
