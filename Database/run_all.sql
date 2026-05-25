-- Master script: runs the entire schema build in dependency order.
-- Idempotent: safe to re-run. Execute from /home/codrin/Practica/Database/.
--
-- Run with sqlcmd, supplying the app login password:
--   sqlcmd -S ... -U sa -P ... -C -b -v AppPassword="<your_pwd>" -i run_all.sql
--
-- In DataGrip: open this file and run it against the server (master),
-- or run each :r file individually in order.

-- ===== Phase 1: schema =====
:r 00_create_database.sql
:r 01_users.sql
:r 02_units.sql
:r 03_categories.sql
:r 04_ingredients.sql
:r 05_recipes.sql
:r 06_recipe_ingredients.sql

-- ===== Phase 1: seeds =====
:r seeds/units_seed.sql
:r seeds/categories_seed.sql
:r seeds/ingredients_seed.sql

-- ===== Phase 2: security state + audit =====
:r 07_users_security.sql
:r 08_audit_log.sql

-- ===== Phase 2.5: FK indexes + RowVersion on Recipes =====
:r 10_phase25_additions.sql

-- ===== Phase 3: meal planning, favorites, pantry =====
:r 11_meal_plan.sql
:r 12_favorites.sql
:r 13_pantry.sql

-- ===== Phase 4: ingredient categories =====
:r 14_ingredient_categories.sql
:r seeds/ingredient_categories_seed.sql

-- ===== Phase H+: recipe drafts + recipe photos =====
:r 15_recipe_drafts.sql
:r 16_recipe_photos.sql

-- ===== Phase 2 + 3 + 4: stored-proc API =====
:r procs/01_users.sql
:r procs/02_recipes_write.sql
:r procs/03_recipes_read.sql
:r procs/04_ingredients.sql
:r procs/05_lookups.sql
:r procs/06_meal_plan.sql
:r procs/07_favorites.sql
:r procs/08_pantry.sql
:r procs/09_shopping_list.sql
:r procs/10_dashboard.sql
:r procs/11_reports.sql
:r procs/12_recipe_drafts.sql
:r procs/13_recipe_photos.sql

-- ===== Phase 2: app login + role (run last; depends on procs + IntList type) =====
:r 09_app_role.sql
