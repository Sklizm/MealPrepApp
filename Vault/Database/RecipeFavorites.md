---
tags: [database, table]
---

# RecipeFavorites

File: `Database/12_favorites.sql`

Many-to-many between [[Users]] and [[Recipes]]. One row = "this user has favorited this recipe".

## Columns
| Column      | Type            | Notes |
|-------------|-----------------|-------|
| UserID      | INT             | FK → [[Users]] **(ON DELETE CASCADE)** |
| RecipeID    | INT             | FK → [[Recipes]] **(ON DELETE CASCADE)** |
| FavoritedAt | DATETIME2(0)    | UTC default |

**Composite PK `(UserID, RecipeID)`** — no separate IDENTITY column. A user can favorite a given recipe at most once.

## Indexes
- `IX_RecipeFavorites_RecipeID` — secondary index for "what users favorited this recipe?" / cascade-from-recipe perf. `UserID` is already the leading column of the PK so no separate UserID index needed.

## Why both FKs cascade
A favorite is meaningless without either side. No multi-cascade-path error because [[Recipes]] → [[Users]] is RESTRICT (no cycle).

## Procs
- `sp_ToggleFavorite` — insert if absent, delete if present. Returns `IsFavorite` 1/0 so the app can update the heart icon directly.
- `sp_GetFavoriteRecipes(@UserID, @PageNumber, @PageSize)` — paged list with the same output shape as `sp_GetRecipes` (VM reuse).

See [[Schema Overview]]
