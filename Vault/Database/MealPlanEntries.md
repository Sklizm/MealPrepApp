---
tags: [database, table]
---

# MealPlanEntries

File: `Database/11_meal_plan.sql`

One row per planned meal: a recipe assigned to a date + meal slot for a user.

## Columns
| Column           | Type            | Notes |
|------------------|-----------------|-------|
| MealPlanEntryID  | INT IDENTITY    | PK |
| UserID           | INT             | FK → [[Users]] (RESTRICT) |
| RecipeID         | INT             | FK → [[Recipes]] **(ON DELETE CASCADE)** |
| CategoryID       | INT             | FK → [[Categories]] (RESTRICT) — the meal slot |
| PlannedDate      | DATE            | day only, no time |
| Servings         | INT             | nullable; NULL = use recipe default. CK > 0 |
| Notes            | NVARCHAR(500)   | nullable |
| CreatedAt        | DATETIME2(0)    | UTC default |

## Indexes
- `IX_MealPlanEntries_UserID_PlannedDate` — leading column UserID for week/month range reads
- `IX_MealPlanEntries_RecipeID` — for the FK + the shopping-list join
- `IX_MealPlanEntries_CategoryID` — FK column index

## Why CategoryID = meal slot
Reusing the existing [[Categories]] table for meal slots avoids a parallel taxonomy. The DB accepts any of the 6 categories; the UI's weekly view chooses to render only 4 columns. See [[Decisions Log]] entry "Meal-slot is a FK to Categories".

## Why RecipeID cascades but UserID doesn't
A meal plan entry has no meaning without its recipe — orphan rows would just be noise on the calendar. Deleting a user with planned meals is the same explicit operation as deleting a user with recipes (consistent with the rest of the schema).

## Procs
- `sp_PlanMeal` — insert
- `sp_UpdatePlannedMeal` — move/re-slot/re-portion (THROW 50002/50003)
- `sp_UnplanMeal` — delete
- `sp_GetWeeklyPlan` — 7 days from a start date
- `sp_GetMonthlyPlan` — entries in a `(year, month)`

Read procs join recipe Title and category Name for direct UI consumption.

See [[Schema Overview]], [[Decisions Log]]
