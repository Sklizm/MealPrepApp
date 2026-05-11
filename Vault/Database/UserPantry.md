---
tags: [database, table]
---

# UserPantry

File: `Database/13_pantry.sql`

What each user currently has in stock, broken down by ingredient + unit.

## Columns
| Column        | Type            | Notes |
|---------------|-----------------|-------|
| UserPantryID  | INT IDENTITY    | PK |
| UserID        | INT             | FK → [[Users]] **(ON DELETE CASCADE)** |
| IngredientID  | INT             | FK → [[Ingredients]] (RESTRICT) |
| UnitID        | INT             | FK → [[Units]] (RESTRICT) |
| Quantity      | DECIMAL(10,2)   | CK > 0 |
| AddedAt       | DATETIME2(0)    | UTC default |
| UpdatedAt     | DATETIME2(0)    | nullable; set by upsert/update |

**`UQ (UserID, IngredientID, UnitID)`** — one row per ingredient+unit per user. The upsert proc MERGEs against this key.

## Indexes
- `IX_UserPantry_IngredientID` and `IX_UserPantry_UnitID` — FK column indexes for the shopping-list join and the RESTRICT delete checks. `UserID` is the leading column of the UQ so a separate index is redundant.

## Why unit-exact (no conversion)
"500 g flour" and "2 cups flour" are two distinct rows; the shopping list joins on exact `(IngredientID, UnitID)`. Cross-unit conversion (weight ↔ volume) needs density tables and is explicitly v2 scope.

## Procs
- `sp_AddPantryItem` — **MERGE upsert** keyed on `(UserID, IngredientID, UnitID)`. Same ingredient+unit again → quantity sums.
- `sp_UpdatePantryQuantity` — absolute set (for "user edited the number").
- `sp_RemovePantryItem` — delete with ownership check.
- `sp_GetPantry` — paged list joined with ingredient + unit names.

## Used by
- `sp_GetShoppingList` — LEFT JOIN to subtract on-hand qty from planned demand.

See [[Schema Overview]], [[Decisions Log]]
