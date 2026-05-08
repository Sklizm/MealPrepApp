---
tags: [database, table, lookup]
---

# Units

File: `Database/02_units.sql`
Seed: `Database/seeds/units_seed.sql`

## Columns
| Column        | Type            | Notes |
|---------------|-----------------|-------|
| UnitID        | INT IDENTITY    | PK |
| Name          | NVARCHAR(50)    | UNIQUE, NOT NULL — e.g. "Gram" |
| Abbreviation  | NVARCHAR(10)    | NOT NULL — e.g. "g" |
| UnitType      | NVARCHAR(20)    | CHECK in (`weight`, `volume`, `count`) |

## Seeded values
weight: g, kg, mg, oz, lb
volume: ml, l, tsp, tbsp, cup
count:  pc (piece), pinch

## Why
- `UnitType` lets the app group dropdowns sensibly and warn on unit-mismatched conversions later.
- CHECK constraint keeps the column from becoming free-text junk.

## Used by
- [[Ingredients]] (DefaultUnitID, optional)
- [[RecipeIngredients]] (UnitID, required)

See [[Schema Overview]]
