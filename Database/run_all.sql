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

-- ===== Phase 2: security state + audit =====
:r 07_users_security.sql
:r 08_audit_log.sql

-- ===== Phase 2: stored-proc API =====
:r procs/01_users.sql
:r procs/02_recipes_write.sql
:r procs/03_recipes_read.sql
:r procs/04_ingredients.sql
:r procs/05_lookups.sql

-- ===== Phase 2: app login + role (run last; depends on procs + IntList type) =====
:r 09_app_role.sql
