USE MealPrepDB;
GO
SET NOCOUNT ON;
DECLARE @U INT,@I INT=(SELECT TOP(1) IngredientID FROM dbo.Ingredients),@G INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'g'),@Kg INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'kg'),@R INT,@Op UNIQUEIDENTIFIER=NEWID(),@RV BINARY(8),@Json NVARCHAR(MAX);
INSERT dbo.Users(Username,Email,PasswordHash) VALUES(N'workflow-fixture',N'workflow@example.invalid',N'fixture'); SET @U=SCOPE_IDENTITY();
SET @Json=(SELECT @I AS IngredientID,@G AS UnitID,CAST(.3333 AS DECIMAL(18,4)) AS Quantity FOR JSON PATH);
EXEC dbo.sp_SaveRecipeBundle @UserID=@U,@OperationID=@Op,@Title=N'Bundle fixture',@Instructions=N'Cook',@Servings=2,@IngredientsJson=@Json,@PhotoMode=N'Set',@ImageData=0x010203,@ThumbnailData=0x0405;
EXEC dbo.sp_SaveRecipeBundle @UserID=@U,@OperationID=@Op,@Title=N'Bundle fixture',@Instructions=N'Cook',@Servings=2,@IngredientsJson=@Json,@PhotoMode=N'Set',@ImageData=0x010203,@ThumbnailData=0x0405;
SELECT @R=RecipeID,@RV=RowVersion FROM dbo.Recipes WHERE UserID=@U;
IF (SELECT COUNT(*) FROM dbo.Recipes WHERE UserID=@U)<>1 OR NOT EXISTS(SELECT 1 FROM dbo.RecipePhotos WHERE RecipeID=@R AND ImageData=0x010203) THROW 51000,N'Bundle photo/create retry failed',1;
IF NOT EXISTS(SELECT 1 FROM dbo.RecipeIngredients WHERE RecipeID=@R AND Quantity=.3333) THROW 51000,N'Fractional recipe quantity rounded',1;
EXEC(N'CREATE OR ALTER TRIGGER dbo.tr_TestBundleFailure ON dbo.AuditLog AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Action=N''RECIPE_BUNDLE'') THROW 51010,N''Injected bundle failure'',1; END');
SET @Op=NEWID();
BEGIN TRY
 EXEC dbo.sp_SaveRecipeBundle @UserID=@U,@OperationID=@Op,@RecipeID=@R,@Title=N'Should roll back',@Instructions=N'Changed',@Servings=3,@IngredientsJson=@Json,@ExpectedVersion=@RV,@PhotoMode=N'Set',@ImageData=0x9999,@ThumbnailData=0x8888;
 THROW 51000,N'Bundle failure ignored',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>51010 THROW; END CATCH;
DROP TRIGGER dbo.tr_TestBundleFailure;
IF NOT EXISTS(SELECT 1 FROM dbo.Recipes WHERE RecipeID=@R AND Title=N'Bundle fixture' AND Instructions=N'Cook' AND Servings=2) OR NOT EXISTS(SELECT 1 FROM dbo.RecipePhotos WHERE RecipeID=@R AND ImageData=0x010203) THROW 51000,N'Photo and recipe were not saved atomically',1;
DECLARE @Name NVARCHAR(150)=(SELECT Name FROM dbo.Ingredients WHERE IngredientID=@I),@Category INT=(SELECT IngredientCategoryID FROM dbo.Ingredients WHERE IngredientID=@I);
SELECT @RV=RowVersion FROM dbo.Ingredients WHERE IngredientID=@I;
EXEC dbo.sp_UpdateIngredient @U,@I,@Name,@Kg,@Category,@RV;
IF NOT EXISTS(SELECT 1 FROM dbo.RecipeIngredients WHERE RecipeID=@R AND UnitID=@G AND Quantity=.3333) THROW 51000,N'Default-unit edit rewrote existing recipe',1;
BEGIN TRY EXEC dbo.sp_UpdateIngredient @U,@I,@Name,@G,@Category,@RV; THROW 51000,N'Stale ingredient edit accepted',1; END TRY
BEGIN CATCH IF ERROR_NUMBER()<>50004 THROW; END CATCH;
EXEC dbo.sp_PlanMeal @U,@R,1,'20300401',2;
DECLARE @Meal INT=(SELECT MealPlanEntryID FROM dbo.MealPlanEntries WHERE UserID=@U),@V BIGINT=(SELECT Version FROM dbo.MealPlanEntries WHERE UserID=@U),@Move INT;
SET @Op=NEWID(); EXEC dbo.sp_MovePlannedMeal @U,@Op,@Meal,@V,'20300402',2;
SELECT @Move=MAX(MoveID) FROM dbo.PlannerMoves WHERE UserID=@U;
SET @Op=NEWID(); EXEC dbo.sp_UndoPlannerMove @U,@Op,@Move;
IF NOT EXISTS(SELECT 1 FROM dbo.MealPlanEntries WHERE MealPlanEntryID=@Meal AND PlannedDate='20300401' AND CategoryID=1) THROW 51000,N'Move undo failed',1;
SELECT @V=Version FROM dbo.MealPlanEntries WHERE MealPlanEntryID=@Meal;
SET @Op=NEWID(); EXEC dbo.sp_MovePlannedMeal @U,@Op,@Meal,@V,'20300402',2;
SELECT @Move=MAX(MoveID) FROM dbo.PlannerMoves WHERE UserID=@U;
EXEC dbo.sp_UpdatePlannedMeal @Meal,@U,3,'20300403',2;
SET @Op=NEWID(); BEGIN TRY EXEC dbo.sp_UndoPlannerMove @U,@Op,@Move; THROW 51000,N'Unsafe move undo accepted',1; END TRY
BEGIN CATCH IF ERROR_NUMBER()<>50004 THROW; END CATCH;
SET @Op=NEWID(); EXEC dbo.sp_SaveWeekTemplate @U,@Op,N'Fixture template','20300401';
DECLARE @Template INT=(SELECT TemplateID FROM dbo.WeekTemplates WHERE UserID=@U),@SourceBatch INT=(SELECT CookingBatchID FROM dbo.MealPlanEntries WHERE MealPlanEntryID=@Meal);
SET @Op=NEWID(); EXEC dbo.sp_ApplyWeekTemplate @U,@Op,@Template,1,'20300408';
EXEC dbo.sp_ApplyWeekTemplate @U,@Op,@Template,1,'20300408';
IF (SELECT COUNT(*) FROM dbo.MealPlanEntries WHERE UserID=@U)<>2 OR EXISTS(SELECT 1 FROM dbo.MealPlanEntries WHERE UserID=@U AND PlannedDate>='20300408' AND CookingBatchID=@SourceBatch) THROW 51000,N'Template retry duplicated or reused old stock',1;
PRINT N'PASS atomic recipe/photo saves, decimal fractions, ingredient concurrency, safe planner undo and fresh template batches';
GO
DECLARE @U INT=(SELECT UserID FROM dbo.Users WHERE Username=N'workflow-fixture'),@R INT=(SELECT RecipeID FROM dbo.Recipes WHERE UserID=(SELECT UserID FROM dbo.Users WHERE Username=N'workflow-fixture')),@I INT=(SELECT TOP(1) IngredientID FROM dbo.RecipeIngredients WHERE RecipeID=(SELECT RecipeID FROM dbo.Recipes WHERE UserID=(SELECT UserID FROM dbo.Users WHERE Username=N'workflow-fixture'))),@G INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'g'),@Op UNIQUEIDENTIFIER=NEWID(),@B INT,@V BIGINT;
UPDATE dbo.RecipeIngredients SET Quantity=200 WHERE RecipeID=@R;
EXEC dbo.sp_PlanCookingBatch @U,@Op,@R,6,N'[{"PlannedDate":"2030-05-06","CategoryID":1,"Portions":2},{"PlannedDate":"2030-05-07","CategoryID":1,"Portions":2}]';
SELECT @B=MAX(CookingBatchID) FROM dbo.CookingBatches WHERE UserID=@U; SELECT @V=Version FROM dbo.CookingBatches WHERE CookingBatchID=@B;
SET @Op=NEWID();EXEC dbo.sp_FinishCookingBatch @U,@Op,@B,@V,6,0,'20300506',NULL,N'[]';
DECLARE @Preview TABLE(RecipeID INT,CategoryID INT,PlannedDate DATE,Servings INT,Notes NVARCHAR(500),RecipeTitle NVARCHAR(150),CategoryName NVARCHAR(100),PreviewToken VARBINARY(32),IsDuplicate BIT);
INSERT @Preview EXEC dbo.sp_PreviewWeekCopy @U,'20300506','20300513';
DECLARE @Token VARBINARY(32)=(SELECT TOP(1) PreviewToken FROM @Preview);
EXEC dbo.sp_CopyWeek @U,'20300506','20300513',@Token;
IF (SELECT COUNT(DISTINCT CookingBatchID) FROM dbo.MealPlanEntries WHERE UserID=@U AND PlannedDate BETWEEN '20300513' AND '20300519')<>1 OR EXISTS(SELECT 1 FROM dbo.MealPlanEntries WHERE UserID=@U AND PlannedDate BETWEEN '20300513' AND '20300519' AND CookingBatchID=@B) THROW 51000,N'Week copy lost batch grouping or reused prepared stock',1;
IF (SELECT NeededQty FROM dbo.fn_BatchShoppingDemand(@U,'20300513','20300519') WHERE IngredientID=@I)<>600 THROW 51000,N'Copied shared batch demand incorrect',1;
SET @Op=NEWID();EXEC dbo.sp_AddInventoryBatch @U,@Op,@I,@G,10;
SET @Op=NEWID();EXEC dbo.sp_AddInventoryBatch @U,@Op,@I,@G,20;
DECLARE @Expected NVARCHAR(MAX)=(SELECT BatchID,Version FROM dbo.InventoryBatches WHERE UserID=@U AND IngredientID=@I AND UnitID=@G AND RemainingQuantity>0 FOR JSON PATH),@ClearOp UNIQUEIDENTIFIER=NEWID();
EXEC dbo.sp_ClearInventoryRows @U,@ClearOp,@I,@G,@Expected;
EXEC dbo.sp_ClearInventoryRows @U,@ClearOp,@I,@G,@Expected;
DECLARE @Events NVARCHAR(MAX)=(SELECT JSON_QUERY(ResultJson,'$.eventIds') FROM dbo.ClientOperations WHERE UserID=@U AND OperationID=@ClearOp);
SET @Op=NEWID();EXEC dbo.sp_UndoStockGroup @U,@Op,@Events;
IF (SELECT Quantity FROM dbo.UserPantry WHERE UserID=@U AND IngredientID=@I AND UnitID=@G)<>30 THROW 51000,N'Atomic stock removal undo failed',1;
PRINT N'PASS week copies preserve shared grouping and create fresh stock; multi-batch removal undo is atomic and idempotent';
GO
DECLARE @U INT=(SELECT UserID FROM dbo.Users WHERE Username=N'workflow-fixture'),@B INT=(SELECT TOP(1) CookingBatchID FROM dbo.CookingBatches WHERE UserID=(SELECT UserID FROM dbo.Users WHERE Username=N'workflow-fixture') AND CookedAt IS NOT NULL ORDER BY CookingBatchID DESC),@V BIGINT,@Op UNIQUEIDENTIFIER=NEWID();
SELECT @V=Version FROM dbo.CookingBatches WHERE CookingBatchID=@B;
EXEC dbo.sp_AllocatePreparedMeals @U,@Op,@B,@V,N'[{"PlannedDate":"2030-05-08","CategoryID":1,"Portions":1},{"PlannedDate":"2030-05-09","CategoryID":1,"Portions":1}]';
EXEC dbo.sp_AllocatePreparedMeals @U,@Op,@B,@V,N'[{"PlannedDate":"2030-05-08","CategoryID":1,"Portions":1},{"PlannedDate":"2030-05-09","CategoryID":1,"Portions":1}]';
IF (SELECT COUNT(*) FROM dbo.MealPlanEntries WHERE CookingBatchID=@B)<>4 OR EXISTS(SELECT 1 FROM dbo.fn_BatchShoppingDemand(@U,'20300508','20300509')) THROW 51000,N'Prepared allocation duplicated or generated raw demand',1;
SELECT @V=Version FROM dbo.CookingBatches WHERE CookingBatchID=@B;SET @Op=NEWID();
BEGIN TRY EXEC dbo.sp_AllocatePreparedMeals @U,@Op,@B,@V,N'[{"PlannedDate":"2030-05-10","CategoryID":1,"Portions":1}]';THROW 51000,N'Prepared portions over-allocated',1;END TRY
BEGIN CATCH IF ERROR_NUMBER()<>50004 THROW;END CATCH;
PRINT N'PASS prepared allocations are idempotent, capacity checked and excluded from raw ingredient demand';
GO
