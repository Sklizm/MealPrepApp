-- Requires the existing nutrition/audit schema. Apply using the backup-first updater.
:On Error exit
:r seeds/moldova_ingredients_seed.sql
:r seeds/catalog_nutrition_seed.sql
