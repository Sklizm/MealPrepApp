---
tags: [database, table]
---

# Ingredients

File: `Database/04_ingredients.sql`

## Columns
| Column          | Type            | Notes |
|-----------------|-----------------|-------|
| IngredientID    | INT IDENTITY    | PK |
| Name            | NVARCHAR(100)   | UNIQUE, NOT NULL |
| DefaultUnitID   | INT             | FK → [[Units]], nullable |

## Why global (no UserID)
For v1, ingredients are shared across all users. "Salt" is "Salt" — no need for every user
to have their own. If we ever need user-private ingredients, add a nullable `UserID`
column later (NULL = global).

## DefaultUnitID
Optional hint — when adding this ingredient to a recipe, pre-fill this unit. The actual
unit is stored per-recipe in [[RecipeIngredients]] so a user can override.

## Used by
- [[RecipeIngredients]] (IngredientID, required)

See [[Schema Overview]], [[Decisions Log]]
