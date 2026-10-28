-- Additive migration; run via scripts/update-database.sh after disposable SQL checks pass.
:On Error exit
USE MealPrepDB;
GO
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.UnitConversions',N'U') IS NULL OR OBJECT_ID(N'dbo.UserPantry',N'U') IS NULL
 THROW 51000,N'Nutrition/pantry baseline required',1;
BEGIN TRAN;
GO
:r procs/03_recipes_read.sql
:r procs/04_ingredients.sql
:r procs/08_pantry.sql
:r 19_shopping_snapshots.sql
:r procs/15_shopping_snapshots.sql
:r procs/16_week_copy.sql
COMMIT;
GO
