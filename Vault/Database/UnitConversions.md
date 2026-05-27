---
tags: [database, table, nutrition]
---

# UnitConversions

File: `Database/17_unit_conversions.sql`

## Purpose
Stores direct compatible unit conversions used by recipe nutrition calculations.

The table deliberately handles only simple same-dimension conversions first, such as grams to kilograms and milliliters to liters. Cross-dimension conversions such as cups of flour to grams are not guessed.

## Columns
| Column | Type | Notes |
|---|---|---|
| FromUnitID | INT | FK -> [[Units]]; part of composite PK |
| ToUnitID | INT | FK -> [[Units]]; part of composite PK |
| Factor | DECIMAL(18,8) | multiply source quantity by this factor to get target quantity |

## Seeded data
`Database/17_unit_conversions.sql` seeds direct conversions for compatible units, including:
- g <-> kg
- ml <-> l
- identity rows used by same-unit nutrition math

## Used by
- `sp_GetRecipeNutrition` converts recipe ingredient quantities into the ingredient nutrition basis unit when a direct conversion exists.
- Missing or incompatible conversions are counted and surfaced as incomplete instead of being estimated.

See [[IngredientNutrition]], [[Schema Overview]], [[Decisions Log]]
