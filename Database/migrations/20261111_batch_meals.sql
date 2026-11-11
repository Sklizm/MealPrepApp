SET XACT_ABORT ON;
BEGIN TRAN;
GO
:r 22_cooking_batches.sql
:r procs/19_cooking_batches.sql
:r procs/20_batch_demand.sql
COMMIT;
GO
