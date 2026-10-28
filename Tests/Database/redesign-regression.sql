-- Disposable database only. Fixtures are preserved to test the additive migration twice.
USE MealPrepDB;
GO
DECLARE @UserID INT=(SELECT UserID FROM dbo.Users WHERE Username=N'regression');
DECLARE @OtherID INT=(SELECT UserID FROM dbo.Users WHERE Username=N'other');
DECLARE @g INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'g');
DECLARE @kg INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'kg');
DECLARE @cup INT=(SELECT TOP(1) UnitID FROM dbo.Units WHERE Abbreviation NOT IN(N'g',N'kg',N'mg',N'oz',N'lb',N'ml',N'l'));
DECLARE @IngredientID INT=(SELECT MIN(IngredientID) FROM dbo.Ingredients);
DECLARE @json NVARCHAR(MAX)=(SELECT @IngredientID AS IngredientID,@g AS UnitID,400 AS Quantity FOR JSON PATH);
DECLARE @New TABLE(ID INT);
INSERT @New EXEC dbo.sp_CreateRecipe @UserID=@UserID,@Title=N'Redesign fixture',@Instructions=N'Prepare',@Servings=4,@IngredientsJson=@json;
DECLARE @RecipeID INT=(SELECT ID FROM @New);
EXEC dbo.sp_PlanMeal @UserID,@RecipeID,1,'20300107',8,NULL;
EXEC dbo.sp_AddPantryItem @UserID,@IngredientID,@kg,0.25;
IF @cup IS NOT NULL EXEC dbo.sp_AddPantryItem @UserID,@IngredientID,@cup,100;
DECLARE @Snapshots TABLE(ID INT);
INSERT @Snapshots EXEC dbo.sp_CreateShoppingSnapshot @UserID,'20300107','20300113';
DECLARE @Snapshot INT=(SELECT ID FROM @Snapshots);
DECLARE @Item INT=(SELECT ItemID FROM dbo.ShoppingSnapshotItems WHERE SnapshotID=@Snapshot);
IF (SELECT COUNT(*) FROM dbo.ShoppingSnapshotItems WHERE SnapshotID=@Snapshot)<>1 THROW 51000,N'Unexpected snapshot rows',1;
IF NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshotItems WHERE ItemID=@Item AND UnitID=@g AND NeededQty=800 AND OnHandQty=250 AND ToBuyQty=550)
 THROW 51000,N'Cross-unit shopping calculation failed',1;
BEGIN TRY
 EXEC dbo.sp_ImportShoppingItem @UserID,@Item;
 THROW 51000,N'Unpurchased import succeeded',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>50000 THROW; END CATCH;
EXEC dbo.sp_SetShoppingPurchased @UserID,@Item,1;
IF EXISTS(SELECT 1 FROM dbo.UserPantry WHERE UserID=@UserID AND IngredientID=@IngredientID AND UnitID=@g) THROW 51000,N'Checking item modified pantry',1;
EXEC dbo.sp_ImportShoppingItem @UserID,@Item;
EXEC dbo.sp_ImportShoppingItem @UserID,@Item;
IF (SELECT Quantity FROM dbo.UserPantry WHERE UserID=@UserID AND IngredientID=@IngredientID AND UnitID=@g)<>550 THROW 51000,N'Import was not idempotent',1;
IF (SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityType=N'ShoppingItem' AND EntityID=@Item AND Action=N'SHOPPING_IMPORT')<>1 THROW 51000,N'Import audit incorrect',1;
BEGIN TRY EXEC dbo.sp_SetShoppingPurchased @OtherID,@Item,0; THROW 51000,N'Cross-user mutation succeeded',1; END TRY
BEGIN CATCH IF ERROR_NUMBER()<>50002 THROW; END CATCH;
DELETE @Snapshots;
INSERT @Snapshots EXEC dbo.sp_CreateShoppingSnapshot @UserID,'20300107','20300113';
IF EXISTS(SELECT 1 FROM dbo.ShoppingSnapshotItems WHERE SnapshotID=(SELECT ID FROM @Snapshots)) THROW 51000,N'Pantry was not subtracted after import',1;
IF NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshotItems WHERE ItemID=@Item AND IsPurchased=1 AND ImportedAt IS NOT NULL) THROW 51000,N'Regeneration lost previous progress',1;
-- Copy week: keep the target meal and skip exact recipe/date/slot duplicates.
EXEC dbo.sp_PlanMeal @UserID,@RecipeID,1,'20300108',4,NULL;
EXEC dbo.sp_PlanMeal @UserID,@RecipeID,1,'20300114',3,N'Preserve me';
DECLARE @Preview TABLE(RecipeID INT,CategoryID INT,PlannedDate DATE,Servings INT,Notes NVARCHAR(500),RecipeTitle NVARCHAR(150),CategoryName NVARCHAR(100),PreviewToken VARBINARY(32),IsDuplicate BIT);
INSERT @Preview EXEC dbo.sp_PreviewWeekCopy @UserID,'20300107','20300114';
IF (SELECT COUNT(*) FROM @Preview WHERE IsDuplicate=1)<>1 THROW 51000,N'Preview duplicate detection failed',1;
DECLARE @Token VARBINARY(32)=(SELECT TOP(1) PreviewToken FROM @Preview);
DECLARE @Created TABLE(CountCreated INT);
INSERT @Created EXEC dbo.sp_CopyWeek @UserID,'20300107','20300114',@Token;
IF (SELECT CountCreated FROM @Created)<>1 THROW 51000,N'Week copy count incorrect',1;
IF NOT EXISTS(SELECT 1 FROM dbo.MealPlanEntries WHERE UserID=@UserID AND PlannedDate='20300114' AND Servings=3 AND Notes=N'Preserve me') THROW 51000,N'Week copy overwrote target',1;
IF NOT EXISTS(SELECT 1 FROM dbo.AuditLog a JOIN dbo.MealPlanEntries e ON e.MealPlanEntryID=a.EntityID WHERE a.Action=N'MEAL_COPY' AND a.UserID=@UserID AND e.PlannedDate='20300115') THROW 51000,N'Week copy not audited',1;
DELETE @Created;
INSERT @Created EXEC dbo.sp_CopyWeek @UserID,'20300107','20300114',@Token;
IF (SELECT CountCreated FROM @Created)<>0 THROW 51000,N'Copy retry duplicated entries',1;
EXEC dbo.sp_PlanMeal @UserID,@RecipeID,2,'20300109',2,NULL;
DECLARE @Before INT=(SELECT COUNT(*) FROM dbo.MealPlanEntries WHERE UserID=@UserID);
BEGIN TRY EXEC dbo.sp_CopyWeek @UserID,'20300107','20300114',@Token; THROW 51000,N'Stale preview accepted',1; END TRY
BEGIN CATCH IF ERROR_NUMBER()<>50004 THROW; END CATCH;
IF (SELECT COUNT(*) FROM dbo.MealPlanEntries WHERE UserID=@UserID)<>@Before THROW 51000,N'Stale copy partially committed',1;
EXECUTE AS USER=N'mealprep_app';
EXEC dbo.sp_GetShoppingSnapshots @UserID;
EXEC dbo.sp_GetShoppingSnapshotItems @UserID,@Snapshot;
REVERT;
-- Volume uses an independent canonical unit; no density conversion from mass.
DECLARE @ml INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'ml');
DECLARE @l INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'l');
DECLARE @Liquid NVARCHAR(MAX)=(SELECT @IngredientID AS IngredientID,@ml AS UnitID,500 AS Quantity FOR JSON PATH);
DELETE @New;
INSERT @New EXEC dbo.sp_CreateRecipe @UserID=@UserID,@Title=N'Volume fixture',@Instructions=N'Prepare',@Servings=2,@IngredientsJson=@Liquid;
DECLARE @LiquidRecipe INT=(SELECT ID FROM @New);
EXEC dbo.sp_PlanMeal @UserID,@LiquidRecipe,1,'20300304',2,NULL;
EXEC dbo.sp_AddPantryItem @UserID,@IngredientID,@l,0.25;
DELETE @Snapshots;
INSERT @Snapshots EXEC dbo.sp_CreateShoppingSnapshot @UserID,'20300304','20300304';
IF NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshotItems WHERE SnapshotID=(SELECT ID FROM @Snapshots) AND UnitID=@ml AND ToBuyQty=250)
 THROW 51000,N'Volume conversion failed',1;
-- Inject a failure after inserts reach the audit table. Both plan rows and audits must roll back.
DELETE @Preview;
INSERT @Preview EXEC dbo.sp_PreviewWeekCopy @UserID,'20300107','20300401';
SET @Token=(SELECT TOP(1) PreviewToken FROM @Preview);
DECLARE @AuditsBefore INT=(SELECT COUNT(*) FROM dbo.AuditLog);
SET @Before=(SELECT COUNT(*) FROM dbo.MealPlanEntries WHERE UserID=@UserID);
EXEC(N'CREATE OR ALTER TRIGGER dbo.tr_TestCopyFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action=N''MEAL_COPY'') THROW 51010,N''Injected copy failure'',1; END');
BEGIN TRY EXEC dbo.sp_CopyWeek @UserID,'20300107','20300401',@Token; THROW 51000,N'Injected failure was ignored',1; END TRY
BEGIN CATCH IF ERROR_NUMBER()<>51010 THROW; END CATCH;
DROP TRIGGER dbo.tr_TestCopyFailure;
IF (SELECT COUNT(*) FROM dbo.MealPlanEntries WHERE UserID=@UserID)<>@Before OR (SELECT COUNT(*) FROM dbo.AuditLog)<>@AuditsBefore
 THROW 51000,N'Failed week copy was not atomic',1;
PRINT N'Redesign SQL regression checks passed';
GO
