---
tags: [database, table]
---

# UserPantry

Fisier: `Database/13_pantry.sql`

Ce are fiecare utilizator in stoc, defalcat pe ingredient + unitate.

## Coloane
| Coloana       | Tip             | Note |
|---------------|-----------------|------|
| UserPantryID  | INT IDENTITY    | PK |
| UserID        | INT             | FK → [[Users-ro]] **(ON DELETE CASCADE)** |
| IngredientID  | INT             | FK → [[Ingredients-ro]] (RESTRICT) |
| UnitID        | INT             | FK → [[Units-ro]] (RESTRICT) |
| Quantity      | DECIMAL(10,2)   | CK > 0 |
| AddedAt       | DATETIME2(0)    | UTC implicit |
| UpdatedAt     | DATETIME2(0)    | nullable; setat de upsert/update |

**`UQ (UserID, IngredientID, UnitID)`** — un rand per ingredient+unitate per utilizator. Procedura de upsert face MERGE pe aceasta cheie.

## Indecsi
- `IX_UserPantry_IngredientID` si `IX_UserPantry_UnitID` — indecsi de coloane FK pentru join-ul listei de cumparaturi si verificarile de stergere RESTRICT. `UserID` este coloana principala a UQ-ului deci un index separat este redundant.

## De ce exact pe unitate (fara conversie)
"500 g faina" si "2 cesti faina" sunt doua randuri distincte; lista de cumparaturi face join pe `(IngredientID, UnitID)` exact. Conversia intre unitati (greutate ↔ volum) are nevoie de tabele de densitate si este explicit scope v2.

## Proceduri
- `sp_AddPantryItem` — **upsert MERGE** pe `(UserID, IngredientID, UnitID)`. Acelasi ingredient+unitate din nou → cantitatea se aduna.
- `sp_UpdatePantryQuantity` — setare absoluta (pentru "utilizatorul a editat numarul").
- `sp_RemovePantryItem` — sterge cu verificare de proprietate.
- `sp_GetPantry` — lista paginata cu join pe numele de ingredient + unitate.

## Folosit de
- `sp_GetShoppingList` — LEFT JOIN pentru a scadea cantitatea din stoc din cererea planificata.

Vezi [[Schema Overview-ro]], [[Decisions Log-ro]]
