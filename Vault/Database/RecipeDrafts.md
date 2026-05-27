---
tags: [database, table, drafts]
---

# RecipeDrafts

File: `Database/15_recipe_drafts.sql`

## Purpose
Stores partially-complete recipes saved from the editor so a user can finish them later.

Drafts are intentionally looser than [[Recipes]]: most content columns are nullable and there are no recipe-completeness CHECK constraints, because a draft may be half-filled.

## Columns
| Column | Type | Notes |
|---|---|---|
| DraftID | INT IDENTITY | PK |
| UserID | INT | FK → [[Users]], NOT NULL, cascade delete |
| CategoryID | INT | FK → [[Categories]], nullable |
| Title | NVARCHAR(150) | nullable |
| Description | NVARCHAR(MAX) | nullable |
| Instructions | NVARCHAR(MAX) | nullable |
| PrepTimeMinutes | INT | nullable |
| CookTimeMinutes | INT | nullable |
| Servings | INT | nullable |
| IngredientsJson | NVARCHAR(MAX) | nullable; opaque JSON blob owned by the app |
| UpdatedAt | DATETIME2(0) | UTC default; updated on save |

## Indexes
- `IX_RecipeDrafts_UserID` — lists a user's drafts quickly.

## Stored procedures
- `sp_SaveDraft` — insert/update draft; returns `DraftID`; owner-only when updating.
- `sp_GetDrafts` — list a user's drafts newest-first.
- `sp_GetDraft` — load one draft in full; owner-only.
- `sp_DeleteDraft` — delete one draft; owner-only.

## Why this shape
- `IngredientsJson` is stored as an opaque blob instead of normalized rows because draft ingredients can be incomplete and invalid while the user is editing.
- Draft rows cascade when the user is deleted because they have no meaning without that user.
- Drafts do not cascade from recipes because they are not recipes yet.

See [[Schema Overview]], [[Decisions Log]]
