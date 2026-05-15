---
tags: [database, table, lookup]
---

# Categories

Fisier: `Database/03_categories.sql`
Seed: `Database/seeds/categories_seed.sql`

## Coloane
| Coloana       | Tip             | Note |
|---------------|-----------------|------|
| CategoryID    | INT IDENTITY    | PK |
| Name          | NVARCHAR(50)    | UNIQUE, NOT NULL |
| Description   | NVARCHAR(255)   | nullable |

## Valori populate (seed)
Breakfast, Lunch, Dinner, Snack, Dessert, Drink

## Folosit de
- [[Recipes-ro]] (CategoryID, optional)

Vezi [[Schema Overview-ro]]
