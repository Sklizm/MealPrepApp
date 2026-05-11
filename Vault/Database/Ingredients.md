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

## Indexes
- `IX_Ingredients_DefaultUnitID` — FK column index (added Phase 2.5).

## Seed
`Database/seeds/ingredients_seed.sql` ships ~44 common items (pantry / oils / dairy / produce / proteins / herbs). `MERGE` keyed on `Name`, with `DefaultUnitID` resolved via `LEFT JOIN dbo.Units ON Abbreviation = ...`.

## Procs
- `sp_AddIngredient`, `sp_GetIngredients`, `sp_SearchIngredients` — basic CRUD-ish surface.
- `sp_GetIngredientUsage` (Phase 2.5) — `RecipeCount` for one ingredient. App calls this before a delete attempt; if > 0, the FK would block the delete anyway, so the app shows a useful message instead.

## Used by
- [[RecipeIngredients]] (IngredientID, required)

See [[Schema Overview]], [[Decisions Log]]
