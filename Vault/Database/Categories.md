---
tags: [database, table, lookup]
---

# Categories

File: `Database/03_categories.sql`
Seed: `Database/seeds/categories_seed.sql`

## Columns
| Column        | Type            | Notes |
|---------------|-----------------|-------|
| CategoryID    | INT IDENTITY    | PK |
| Name          | NVARCHAR(50)    | UNIQUE, NOT NULL |
| Description   | NVARCHAR(255)   | nullable |

## Seeded values
Breakfast, Lunch, Dinner, Snack, Dessert, Drink

## Used by
- [[Recipes]] (CategoryID, optional)

See [[Schema Overview]]
