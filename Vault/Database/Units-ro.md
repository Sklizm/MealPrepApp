---
tags: [database, table, lookup]
---

# Units

Fisier: `Database/02_units.sql`
Seed: `Database/seeds/units_seed.sql`

## Coloane
| Coloana       | Tip             | Note |
|---------------|-----------------|------|
| UnitID        | INT IDENTITY    | PK |
| Name          | NVARCHAR(50)    | UNIQUE, NOT NULL — ex. "Gram" |
| Abbreviation  | NVARCHAR(10)    | NOT NULL — ex. "g" |
| UnitType      | NVARCHAR(20)    | CHECK in (`weight`, `volume`, `count`) |

## Valori populate (seed)
weight: g, kg, mg, oz, lb
volume: ml, l, tsp, tbsp, cup
count:  pc (piece), pinch

## De ce
- `UnitType` permite aplicatiei sa grupeze dropdown-urile rezonabil si sa avertizeze la conversii cu unitati neconcordante mai tarziu.
- Constrangerea CHECK opreste coloana sa devina text liber.

## Folosit de
- [[Ingredients-ro]] (DefaultUnitID, optional)
- [[RecipeIngredients-ro]] (UnitID, obligatoriu)

Vezi [[Schema Overview-ro]]
