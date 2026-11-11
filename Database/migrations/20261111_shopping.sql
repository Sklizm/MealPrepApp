SET XACT_ABORT ON;
BEGIN TRAN;
GO
-- Prerequisite: 20261111_inventory.sql. Use the backup-first updater.
:r 21_shopping_changes.sql
:r procs/18_shopping_changes.sql

COMMIT;
GO
