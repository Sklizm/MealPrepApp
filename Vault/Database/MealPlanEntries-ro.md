---
tags: [database, table]
---

# MealPlanEntries

Fisier: `Database/11_meal_plan.sql`

Un rand per masa planificata: o reteta atribuita unei date + slot de masa pentru un utilizator.

## Coloane
| Coloana          | Tip             | Note |
|------------------|-----------------|------|
| MealPlanEntryID  | INT IDENTITY    | PK |
| UserID           | INT             | FK → [[Users-ro]] (RESTRICT) |
| RecipeID         | INT             | FK → [[Recipes-ro]] **(ON DELETE CASCADE)** |
| CategoryID       | INT             | FK → [[Categories-ro]] (RESTRICT) — slot-ul de masa |
| PlannedDate      | DATE            | doar zi, fara ora |
| Servings         | INT             | nullable; NULL = foloseste valoarea implicita a retetei. CK > 0 |
| Notes            | NVARCHAR(500)   | nullable |
| CreatedAt        | DATETIME2(0)    | UTC implicit |

## Indecsi
- `IX_MealPlanEntries_UserID_PlannedDate` — coloana principala UserID pentru citiri de interval saptamana/luna
- `IX_MealPlanEntries_RecipeID` — pentru FK + join-ul listei de cumparaturi
- `IX_MealPlanEntries_CategoryID` — index de coloana FK

## De ce CategoryID = slot de masa
Refolosirea tabelului existent [[Categories-ro]] pentru sloturile de masa evita o taxonomie paralela. DB-ul accepta oricare dintre cele 6 categorii; view-ul saptamanal al UI-ului alege sa randeze doar 4 coloane. Vezi intrarea din [[Decisions Log-ro]] "Meal-slot is a FK to Categories".

## De ce RecipeID face cascada dar UserID nu
O intrare de plan de masa nu are sens fara reteta sa — randurile orfane ar fi doar zgomot pe calendar. Stergerea unui utilizator cu mese planificate este aceeasi operatie explicita ca stergerea unui utilizator cu retete (consistent cu restul schemei).

## Proceduri
- `sp_PlanMeal` — insert
- `sp_UpdatePlannedMeal` — muta/re-asaza/re-portioneaza (THROW 50002/50003)
- `sp_UnplanMeal` — delete
- `sp_GetWeeklyPlan` — 7 zile de la o data de start
- `sp_GetMonthlyPlan` — intrari intr-un `(an, luna)`

Procedurile de citire fac join cu Title-ul retetei si Name-ul categoriei pentru consum direct in UI.

Vezi [[Schema Overview-ro]], [[Decisions Log-ro]]
