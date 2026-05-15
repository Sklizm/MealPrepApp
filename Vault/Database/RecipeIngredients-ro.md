---
tags: [database, table, junction]
---

# RecipeIngredients

Fisier: `Database/06_recipe_ingredients.sql`

Tabel de jonctiune: care ingrediente (si cat de mult) intra in fiecare reteta.

## Coloane
| Coloana             | Tip             | Note |
|---------------------|-----------------|------|
| RecipeIngredientID  | INT IDENTITY    | PK |
| RecipeID            | INT             | FK → [[Recipes-ro]], `ON DELETE CASCADE` |
| IngredientID        | INT             | FK → [[Ingredients-ro]] |
| UnitID              | INT             | FK → [[Units-ro]] |
| Quantity            | DECIMAL(10,2)   | CHECK > 0 |
| Notes               | NVARCHAR(255)   | nullable — "tocat marunt", "dupa gust" |

## Constrangeri
- `UNIQUE (RecipeID, IngredientID)` — un ingredient apare cel mult o data per reteta.
  Daca ai nevoie de "1 lingura ulei de masline pentru aluat + 1 lingura pentru tigaie", combina-le sau foloseste Notes.
- `Quantity > 0` — zero sau negativ nu are sens.

## Index
- `IX_RecipeIngredients_IngredientID` — "ce retete folosesc oua?" rapid
  (PK-ul deja serveste compusul cu RecipeID principal via constrangerea unica.)

## Cascada
Stergerea unei [[Recipes-ro|Retete]] elimina automat randurile sale de ingrediente — nu au
sens fara reteta parinte. Aceasta este **singura** cascada din schema.

Vezi [[Schema Overview-ro]], [[Decisions Log-ro]]
