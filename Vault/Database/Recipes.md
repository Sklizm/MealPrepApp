---
tags: [database, table]
---

# Recipes

File: `Database/05_recipes.sql`

## Columns
| Column           | Type            | Notes |
|------------------|-----------------|-------|
| RecipeID         | INT IDENTITY    | PK |
| UserID           | INT             | FK → [[Users]], NOT NULL |
| CategoryID       | INT             | FK → [[Categories]], nullable |
| Title            | NVARCHAR(150)   | NOT NULL |
| Description      | NVARCHAR(MAX)   | nullable |
| Instructions     | NVARCHAR(MAX)   | NOT NULL |
| PrepTimeMinutes  | INT             | nullable, CHECK >= 0 |
| CookTimeMinutes  | INT             | nullable, CHECK >= 0 |
| Servings         | INT             | nullable, CHECK > 0 |
| CreatedAt        | DATETIME2(0)    | UTC default |
| UpdatedAt        | DATETIME2(0)    | nullable; app updates on save |

## Indexes
- `IX_Recipes_UserID` — fast "list my recipes"
- `IX_Recipes_CategoryID` — fast "all dinners"

## Why this shape
- `Instructions` as `NVARCHAR(MAX)` — recipes can get long; markdown stored as-is.
- Times are nullable because not every recipe has both prep and cook (e.g. a salad).
- No cascade from Users → Recipes: deleting a user with recipes should require explicit
  cleanup, not silent data loss.

## Used by
- [[RecipeIngredients]] (FK with `ON DELETE CASCADE`)

See [[Schema Overview]], [[Decisions Log]]
