---
tags: [database, table]
---

# IngredientCategories

File: `Database/14_ingredient_categories.sql`

Lookup table for grouping [[Ingredients]] in the app's Ingrediente sidebar (Produse / Lactate / Carne / Conserve / Condimente / Cereale / Bauturi / Altele).

## Columns
| Column                | Type            | Notes |
|-----------------------|-----------------|-------|
| IngredientCategoryID  | INT IDENTITY    | PK |
| Name                  | NVARCHAR(50)    | UNIQUE, NOT NULL |

8 rows seeded in `seeds/ingredient_categories_seed.sql`.

## Related: `Ingredients.IngredientCategoryID` (nullable FK)
Added as a nullable column on [[Ingredients]] so existing rows survive the migration. The seed file then UPDATEs the 44 shipped ingredients with sensible category assignments. New user-added ingredients carry NULL until a category is chosen.

Cascade: RESTRICT (consistent with the lookup-table rule — Categories/Units never cascade).

## Why this exists
The app design's Ingrediente sidebar has a "Categorii" entry. Without this table, that entry would be a UI grouping with no real data. Now it's backed by a proper FK, sortable / filterable / extensible. Doesn't replace the original "ingredients are global" decision — it augments it.

## Procs
- `sp_GetIngredientCategories` — list the 8 (or however many) categories.
- `sp_GetIngredients(@IngredientCategoryID = NULL)` — optional filter; NULL = all.

## Used by
- The "Categorii" sub-view of the Ingrediente tab in the app.

See [[Schema Overview]], [[Decisions Log]]
