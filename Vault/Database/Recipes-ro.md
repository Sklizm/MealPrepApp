---
tags: [database, table]
---

# Recipes

Fisier: `Database/05_recipes.sql`

## Coloane
| Coloana          | Tip             | Note |
|------------------|-----------------|------|
| RecipeID         | INT IDENTITY    | PK |
| UserID           | INT             | FK → [[Users-ro]], NOT NULL |
| CategoryID       | INT             | FK → [[Categories-ro]], nullable |
| Title            | NVARCHAR(150)   | NOT NULL |
| Description      | NVARCHAR(MAX)   | nullable |
| Instructions     | NVARCHAR(MAX)   | NOT NULL |
| PrepTimeMinutes  | INT             | nullable, CHECK >= 0 |
| CookTimeMinutes  | INT             | nullable, CHECK >= 0 |
| Servings         | INT             | nullable, CHECK > 0 |
| CreatedAt        | DATETIME2(0)    | UTC implicit |
| UpdatedAt        | DATETIME2(0)    | nullable; aplicatia actualizeaza la salvare |
| RowVersion       | ROWVERSION      | mentinut automat; token de concurenta optimista |

## Indecsi
- `IX_Recipes_UserID` — "listeaza retetele mele" rapid
- `IX_Recipes_CategoryID` — "toate cinele" rapid

## Concurenta optimista
`RowVersion` este adaugat de `Database/10_phase25_additions.sql`. `sp_GetRecipeFull` il returneaza; `sp_UpdateRecipe` il cere ca `@RowVersion BINARY(8)` si `THROW 50004` daca nu se potriveste — protejeaza impotriva unui al doilea tab care suprascrie schimbarile mai recente. SQL Server mentine coloana automat; nu o seta sau actualiza manual.

## De ce aceasta forma
- `Instructions` ca `NVARCHAR(MAX)` — retetele pot deveni lungi; markdown stocat ca atare.
- Timpii sunt nullable deoarece nu fiecare reteta are atat prep cat si gatit (ex. o salata).
- Fara cascada de la Users → Recipes: stergerea unui utilizator cu retete ar trebui sa necesite curatare
  explicita, nu pierdere silentioasa de date.

## Folosit de
- [[RecipeIngredients-ro]] (FK cu `ON DELETE CASCADE`)

Vezi [[Schema Overview-ro]], [[Decisions Log-ro]]
