SET XACT_ABORT ON;
BEGIN TRAN;
GO
-- Prerequisite: 20261028_cooking_redesign.sql. Run only through the backup-first updater.
:r 20_inventory_batches.sql
:r procs/17_inventory.sql

COMMIT;
GO
