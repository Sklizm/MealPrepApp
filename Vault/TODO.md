---
tags: [todo]
---

# TODO

Running checklist. Check things off as they're done. Latest items at the top of each section.

## Now
- [ ] Refresh DataGrip and confirm the 8 tables + 18 procs + `IntList` type all show up
- [ ] Decide what (if anything) to seed for [[Ingredients]] (currently empty)
- [ ] Wire the .NET app to connect as `mealprep_app` (not `sa`) — connection string goes in `App/`'s config

## Soon
- [ ] Decide on EF Core vs Dapper (the proc API works with either; defer until the app side begins)
- [ ] Optional: add convenience views for ad-hoc DataGrip exploration (read-only, separate role grants if exposed to the app)
- [ ] Optional: add an admin role for migrations distinct from `sa`

## Maybe Later (out of v1 scope, see [[Decisions Log]])
- [ ] Meal plans / weekly schedule tables
- [ ] Shopping list generation
- [ ] Nutrition tracking (calories, macros per ingredient)
- [ ] Recipe ratings / favorites
- [ ] Photos / image attachments
- [ ] User-private ingredients (add nullable UserID to [[Ingredients]])

## Done
- [x] Phase 2 — App API + security layer: 18 stored procs, `mealprep_app` low-priv login, `DENY` on direct DML, audit log, password history, lockout (5/15min)
- [x] End-to-end `run_all.sql` runs clean and idempotent
- [x] Verified app login can EXEC procs but cannot SELECT/INSERT/UPDATE/DELETE tables directly (ownership chaining covers mutations)
- [x] Ran `run_all.sql` against the Docker container via `sqlcmd` — all 6 tables created, 12 units + 6 categories seeded
- [x] Verified CHECK constraints fire (Quantity > 0, PrepTime/CookTime >= 0, Servings > 0, UnitType in allowed set)
- [x] Verified UNIQUE constraints fire (Users.Username, RecipeIngredients(RecipeID, IngredientID))
- [x] Verified `ON DELETE CASCADE` on Recipes → RecipeIngredients works
- [x] Verified RESTRICT on Ingredients delete (blocks if in use)
- [x] Project folder structure
- [x] `00_create_database.sql` — `MealPrepDB`
- [x] `01_users.sql` — [[Users]]
- [x] `02_units.sql` — [[Units]]
- [x] `03_categories.sql` — [[Categories]]
- [x] `04_ingredients.sql` — [[Ingredients]]
- [x] `05_recipes.sql` — [[Recipes]]
- [x] `06_recipe_ingredients.sql` — [[RecipeIngredients]]
- [x] `seeds/units_seed.sql`, `seeds/categories_seed.sql`
- [x] `run_all.sql` master script
- [x] Obsidian vault scaffolded
