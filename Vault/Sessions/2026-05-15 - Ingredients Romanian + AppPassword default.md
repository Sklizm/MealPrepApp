---
tags: [session]
---

# 2026-05-15 — Ingredients translated to Romanian + AppPassword default

## Trigger

The app will ship in Romanian (English version is a possible later effort). The 44 seeded ingredients were still English, so they got translated. Did a clean DB rebuild to land the new names in place rather than leave both English and Romanian rows side by side. Hit a small ergonomics paper-cut in `09_app_role.sql` while doing it (the `-v AppPassword=...` requirement on every rebuild) and fixed that too.

## Changes

### `Database/seeds/ingredients_seed.sql` — Romanian names
- All 44 ingredients renamed to Romanian, written without diacritics to match the convention already used in [[IngredientCategories]] (`Lactate si oua`, not `Lactate și ouă`).
- Default units unchanged.
- A few translation choices worth noting:
  - `Cream` → `Smantana` (the everyday Romanian default; not `Frișcă`).
  - `Paprika` → `Boia de ardei`.
  - `Cheese` → `Branza` (generic; specific types like `Cascaval`/`Telemea` are not seeded).
- An English-language version of the app will need a localization layer rather than alternate seed rows.

### `Database/seeds/ingredient_categories_seed.sql` — JOIN updated
- The category-assignment VALUES block's `Name` side rewritten to the new Romanian names so the backfill still matches.
- Category names themselves were already Romanian — no change there.

### Clean rebuild
- Because `MERGE` is keyed on `Name`, simply changing the seed would *add* Romanian rows alongside the old English ones rather than rename. Chose the clean rebuild path (drop + recreate) since there was no real test data to preserve.
- Sequence:
  ```bash
  sqlcmd ... -d master -Q "ALTER DATABASE MealPrepDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE MealPrepDB;"
  docker exec -u 0 MealPrepDB rm -rf /tmp/Database
  docker cp Database MealPrepDB:/tmp/Database
  sqlcmd ... -i run_all.sql
  ```
- The `mealprep_app` login persists at the *server* level across a database drop, so its password doesn't need to be re-supplied on rebuilds.

### `Database/09_app_role.sql` — `AppPassword` defaults to empty
- Added `:setvar AppPassword ""` so the sqlcmd preprocessor doesn't error on undefined variable during a rebuild.
- The `CREATE LOGIN ... WITH PASSWORD = N'$(AppPassword)'` branch only fires when the login doesn't already exist, so on rebuilds the empty default is unused.
- Header comment rewritten to spell out the two paths (first-time setup vs rebuild) and to flag the sqlcmd precedence gotcha: a `:setvar` *in a script* overrides `-v` *from the command line*. To supply the password via `-v` on first-time setup, the `:setvar AppPassword ""` line must be deleted (or its value edited in place).

## Verification

```sql
SELECT c.Name AS Categorie, i.Name AS Ingredient, u.Abbreviation AS UM
FROM dbo.Ingredients i
LEFT JOIN dbo.IngredientCategories c ON c.IngredientCategoryID = i.IngredientCategoryID
LEFT JOIN dbo.Units u ON u.UnitID = i.DefaultUnitID
ORDER BY c.Name, i.Name;
```

- 44 rows.
- Every ingredient has a non-null category — backfill landed cleanly.
- Spot-checks: `Sare` → Condimente si ierburi, `Ou` → Lactate si oua, `Piept de pui` → Carne si peste, `Ulei de masline` → Conserve, `Orez` → Cereale si paste, `Rosie` → Produse.
- Re-ran `run_all.sql` without `-v AppPassword=...` — clean exit, all `Changed database context` lines, no `scripting variable not defined` error.

## What was NOT changed

- `Database/04_ingredients.sql` (the table definition) — schema is identical; only the seed data changed.
- The `Categories` table (recipe categories: Breakfast / Lunch / Dinner / Snack / Dessert / Drink) is still English. Translating it is a separate decision — it'd affect the meal-slot column in [[MealPlanEntries]] and the printed weekly view.
- Per-table notes under `Vault/Database/` — schema notes stay accurate; only the seed sample names would need updating in [[Ingredients]] if we want the note to mirror the data exactly.

## Next

- App-side: when the WPF screens start binding to `sp_GetIngredients`, they'll see Romanian names directly. No client-side translation needed.
- Optional follow-up: translate `Categories` (recipe categories) too if the app's meal-slot labels should be Romanian end-to-end.
