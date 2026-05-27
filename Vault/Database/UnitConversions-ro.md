---
tags: [database, table, nutrition, ro]
---

# UnitConversions (Romana)

Fisier: `Database/17_unit_conversions.sql`

## Scop
Stocheaza conversii directe intre unitati compatibile, folosite de calculul nutritiei pe reteta.

Tabelul trateaza intentionat doar conversii simple in aceeasi dimensiune, cum ar fi grame in kilograme si mililitri in litri. Conversiile intre dimensiuni diferite, de exemplu cani de faina in grame, nu sunt ghicite.

## Coloane
| Coloana | Tip | Note |
|---|---|---|
| FromUnitID | INT | FK -> [[Units-ro]]; parte din PK compozit |
| ToUnitID | INT | FK -> [[Units-ro]]; parte din PK compozit |
| Factor | DECIMAL(18,8) | inmulteste cantitatea sursa cu acest factor pentru cantitatea tinta |

## Date seed
`Database/17_unit_conversions.sql` adauga conversii directe pentru unitati compatibile, inclusiv:
- g <-> kg
- ml <-> l
- randuri identitate folosite de calculul nutritiv cu aceeasi unitate

## Folosit de
- `sp_GetRecipeNutrition` converteste cantitatile ingredientelor din reteta in unitatea-baza a nutritiei ingredientului cand exista o conversie directa.
- Conversiile lipsa sau incompatibile sunt numarate si afisate ca date incomplete, nu estimate fals.

Vezi [[IngredientNutrition-ro]], [[Schema Overview-ro]], [[Decisions Log-ro]]
