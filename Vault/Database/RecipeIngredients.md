---
tags: [database, table, junction]
---

# RecipeIngredients

File: `Database/06_recipe_ingredients.sql`

Junction table: which ingredients (and how much) go into each recipe.

## Columns
| Column              | Type            | Notes |
|---------------------|-----------------|-------|
| RecipeIngredientID  | INT IDENTITY    | PK |
| RecipeID            | INT             | FK → [[Recipes]], `ON DELETE CASCADE` |
| IngredientID        | INT             | FK → [[Ingredients]] |
| UnitID              | INT             | FK → [[Units]] |
| Quantity            | DECIMAL(10,2)   | CHECK > 0 |
| Notes               | NVARCHAR(255)   | nullable — "diced", "to taste" |

## Constraints
- `UNIQUE (RecipeID, IngredientID)` — an ingredient appears at most once per recipe.
  If you need "1 tbsp olive oil for the dough + 1 tbsp for the pan", combine them or use Notes.
- `Quantity > 0` — zero or negative makes no sense.

## Index
- `IX_RecipeIngredients_IngredientID` — fast "what recipes use eggs?"
  (The PK already serves the RecipeID-leading composite via the unique constraint.)

## Cascade
Deleting a [[Recipes|Recipe]] removes its ingredient rows automatically — they have no
meaning without their parent recipe. This is the **only** cascade in the schema.

See [[Schema Overview]], [[Decisions Log]]
