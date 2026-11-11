USE MealPrepDB;
GO
SET NOCOUNT ON;
DECLARE @U INT,@I INT=(SELECT TOP(1) IngredientID FROM dbo.Ingredients),@G INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'g'),@R INT,@B INT,@Stock INT,@V BIGINT,@Op UNIQUEIDENTIFIER=NEWID(),@FinishOp UNIQUEIDENTIFIER=NEWID(),@StartVersion BIGINT,@Meal INT;
INSERT dbo.Users(Username,Email,PasswordHash) VALUES(N'cooking-batch-fixture',N'cooking-batch@example.invalid',N'fixture'); SET @U=SCOPE_IDENTITY();
INSERT dbo.Recipes(UserID,Title,Instructions,Servings) VALUES(@U,N'Batch fixture',N'Cook',2); SET @R=SCOPE_IDENTITY();
INSERT dbo.RecipeIngredients(RecipeID,IngredientID,UnitID,Quantity) VALUES(@R,@I,@G,200);
EXEC dbo.sp_AddInventoryBatch @U,@Op,@I,@G,1000;
SELECT @Stock=MAX(BatchID) FROM dbo.InventoryBatches WHERE UserID=@U;
SET @Op=NEWID();
EXEC dbo.sp_PlanCookingBatch @U,@Op,@R,6,N'[{"PlannedDate":"2030-01-07","CategoryID":1,"Portions":2},{"PlannedDate":"2030-01-08","CategoryID":1,"Portions":2},{"PlannedDate":"2030-01-09","CategoryID":1,"Portions":2}]';
EXEC dbo.sp_PlanCookingBatch @U,@Op,@R,6,N'[{"PlannedDate":"2030-01-07","CategoryID":1,"Portions":2},{"PlannedDate":"2030-01-08","CategoryID":1,"Portions":2},{"PlannedDate":"2030-01-09","CategoryID":1,"Portions":2}]';
SELECT @B=MAX(CookingBatchID),@V=MAX(Version) FROM dbo.CookingBatches WHERE UserID=@U;
IF (SELECT COUNT(*) FROM dbo.CookingBatches WHERE UserID=@U)<>1 THROW 51000,N'Batch plan retry duplicated',1;
IF (SELECT NeededQty FROM dbo.fn_BatchShoppingDemand(@U,'20300107','20300113') WHERE IngredientID=@I)<>600 THROW 51000,N'Batch ingredients were counted more than once',1;
DECLARE @Deductions NVARCHAR(MAX)=(SELECT @Stock AS BatchID,CONVERT(BIGINT,1) AS ExpectedVersion,600 AS Quantity FOR JSON PATH);
SET @StartVersion=@V;
EXEC dbo.sp_FinishCookingBatch @U,@FinishOp,@B,@StartVersion,6,2,'20300107','20300111',@Deductions;
EXEC dbo.sp_FinishCookingBatch @U,@FinishOp,@B,@StartVersion,6,2,'20300107','20300111',@Deductions;
IF (SELECT RemainingQuantity FROM dbo.InventoryBatches WHERE BatchID=@Stock)<>400 THROW 51000,N'Cooking deductions duplicated',1;
IF EXISTS(SELECT 1 FROM dbo.fn_BatchShoppingDemand(@U,'20300107','20300113')) THROW 51000,N'Prepared meals generated raw ingredient demand',1;
IF NOT EXISTS(SELECT 1 FROM dbo.CookingBatches WHERE CookingBatchID=@B AND ProducedPortions=6 AND UsedPortions=2 AND RecipeSnapshot IS NOT NULL AND NutritionSnapshot IS NOT NULL) THROW 51000,N'Cooking snapshot or production missing',1;
UPDATE dbo.Recipes SET Title=N'Changed published title' WHERE RecipeID=@R;
IF JSON_VALUE((SELECT RecipeSnapshot FROM dbo.CookingBatches WHERE CookingBatchID=@B),'$.Title')<>N'Batch fixture' THROW 51000,N'Historical snapshot changed',1;
SELECT @V=Version FROM dbo.CookingBatches WHERE CookingBatchID=@B;
SELECT @Meal=MealPlanEntryID FROM dbo.MealPlanEntries WHERE CookingBatchID=@B AND PlannedDate='20300108';
SET @Op=NEWID(); EXEC dbo.sp_RecordPortionEvent @U,@Op,@B,@V,2,N'Consumed',@Meal;
IF (SELECT RemainingQuantity FROM dbo.InventoryBatches WHERE BatchID=@Stock)<>400 THROW 51000,N'Leftovers deducted raw stock again',1;
SELECT @V=Version FROM dbo.CookingBatches WHERE CookingBatchID=@B;
SET @Op=NEWID();
BEGIN TRY EXEC dbo.sp_RecordPortionEvent @U,@Op,@B,@V,1,N'Discarded'; THROW 51000,N'Reserved leftovers discarded',1; END TRY
BEGIN CATCH IF ERROR_NUMBER()<>50004 THROW; END CATCH;
DELETE dbo.MealPlanEntries WHERE CookingBatchID=@B AND PlannedDate='20300109';
SELECT @V=Version FROM dbo.CookingBatches WHERE CookingBatchID=@B;
SET @Op=NEWID(); EXEC dbo.sp_RecordPortionEvent @U,@Op,@B,@V,1,N'Discarded';
IF (SELECT SUM(Portions) FROM dbo.PortionEvents WHERE CookingBatchID=@B AND Kind=N'Consumed')<>4 OR (SELECT SUM(Portions) FROM dbo.PortionEvents WHERE CookingBatchID=@B AND Kind=N'Discarded')<>1 THROW 51000,N'Explicit consumption/waste history incorrect',1;
DECLARE @Before INT=(SELECT COUNT(*) FROM dbo.MealPlanEntries WHERE UserID=@U);
SET @Op=NEWID();
BEGIN TRY EXEC dbo.sp_PlanCookingBatch @U,@Op,@R,1,N'[{"PlannedDate":"2030-02-01","CategoryID":1,"Portions":2}]'; THROW 51000,N'Over-allocation accepted',1; END TRY
BEGIN CATCH IF ERROR_NUMBER()<>50000 THROW; END CATCH;
IF (SELECT COUNT(*) FROM dbo.MealPlanEntries WHERE UserID=@U)<>@Before THROW 51000,N'Invalid plan partially committed',1;
PRINT N'PASS shared batch demand, idempotent cooking, immutable snapshots, leftovers, reservations and explicit waste';
GO
