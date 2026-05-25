---
tags: [database, table, photos]
---

# RecipePhotos

File: `Database/16_recipe_photos.sql`

## Purpose
Stores one optional photo per recipe.

Image bytes live in SQL Server so they travel with the database across machines and stay inside the stored-procedure-only app API. The WPF app downscales and re-encodes selected files before saving them.

## Columns
| Column | Type | Notes |
|---|---|---|
| RecipeID | INT | PK and FK → [[Recipes]], cascade delete |
| ImageData | VARBINARY(MAX) | JPEG bytes supplied by the app |
| ContentType | NVARCHAR(100) | currently saved as `image/jpeg` by the app |
| UpdatedAt | DATETIME2(0) | UTC default; updated on replacement |

## Relationships
- `RecipeID` is both the primary key and the foreign key to [[Recipes]].
- This enforces one photo per recipe.
- Deleting a recipe cascade-deletes its photo.

## Stored procedures
- `sp_SetRecipePhoto` — insert or replace the recipe photo; owner-only.
- `sp_GetRecipePhoto` — read photo bytes/content type; returns no rows if absent.
- `sp_DeleteRecipePhoto` — remove photo; owner-only; silent if no photo exists.

## App-side handling
- Selected images are decoded through WPF imaging.
- Images are downscaled with `DecodePixelWidth = 1200`.
- Images are re-encoded to JPEG quality 85 before calling `sp_SetRecipePhoto`.
- Recipe cards load photo bytes and convert them with `ByteArrayToImageSourceConverter`.

See [[Schema Overview]], [[Recipes]], [[Decisions Log]]
