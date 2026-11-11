USE MealPrepDB;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @User INT,@Other INT,@Ingredient INT=(SELECT TOP(1) IngredientID FROM dbo.Ingredients ORDER BY IngredientID),@Unit INT=(SELECT TOP(1) UnitID FROM dbo.Units WHERE Abbreviation=N'g'),@Batch INT,@Version BIGINT,@Event INT,@Op UNIQUEIDENTIFIER=NEWID();
INSERT dbo.Users(Username,Email,PasswordHash) VALUES(N'inventory-fixture',N'inventory@example.invalid',N'fixture'); SET @User=SCOPE_IDENTITY();
INSERT dbo.Users(Username,Email,PasswordHash) VALUES(N'inventory-other',N'inventory-other@example.invalid',N'fixture'); SET @Other=SCOPE_IDENTITY();
EXEC dbo.sp_AddPantryItem @User,@Ingredient,@Unit,125.50;
IF (SELECT SUM(RemainingQuantity) FROM dbo.InventoryBatches WHERE UserID=@User)<>125.50 THROW 51000,N'Legacy add did not create stock',1;
IF EXISTS(SELECT 1 FROM dbo.InventoryBatches WHERE UserID=@User AND (PurchasedOn IS NOT NULL OR ExpiresOn IS NOT NULL)) THROW 51000,N'Legacy dates invented',1;
EXEC dbo.sp_AddInventoryBatch @User,@Op,@Ingredient,@Unit,100.1250,'2026-11-11','2026-11-15';
EXEC dbo.sp_AddInventoryBatch @User,@Op,@Ingredient,@Unit,100.1250,'2026-11-11','2026-11-15';
IF (SELECT Quantity FROM dbo.UserPantry WHERE UserID=@User)<>225.6250 THROW 51000,N'Import duplicated or decimal rounded',1;
IF (SELECT COUNT(*) FROM dbo.InventoryBatches WHERE UserID=@User)<>2 THROW 51000,N'Duplicate operation added stock',1;
SELECT TOP(1) @Batch=BatchID,@Version=Version FROM dbo.InventoryBatches WHERE UserID=@User AND ExpiresOn IS NOT NULL;
SET @Op=NEWID(); EXEC dbo.sp_AdjustInventoryBatch @User,@Op,@Batch,@Version,-25.1250,N'Correction';
SELECT @Event=MAX(EventID) FROM dbo.StockEvents WHERE BatchID=@Batch;
SET @Op=NEWID(); EXEC dbo.sp_UndoStockEvent @User,@Op,@Event;
IF (SELECT RemainingQuantity FROM dbo.InventoryBatches WHERE BatchID=@Batch)<>100.1250 THROW 51000,N'Undo failed',1;
DECLARE @Pantry INT=(SELECT UserPantryID FROM dbo.UserPantry WHERE UserID=@User);
EXEC dbo.sp_UpdatePantryQuantity @Pantry,@User,50;
IF (SELECT SUM(RemainingQuantity) FROM dbo.InventoryBatches WHERE UserID=@User)<>50 THROW 51000,N'Legacy absolute correction failed',1;
EXEC dbo.sp_RemovePantryItem @Pantry,@User;
IF EXISTS(SELECT 1 FROM dbo.InventoryBatches WHERE UserID=@User AND RemainingQuantity<>0) THROW 51000,N'Legacy removal left stock',1;
IF NOT EXISTS(SELECT 1 FROM dbo.StockEvents WHERE UserID=@User AND QuantityDelta<0) THROW 51000,N'Missing correction events',1;
ROLLBACK;
PRINT N'PASS inventory preservation, decimal quantities, idempotency, undo, legacy corrections and removal';
GO
-- Error tests each use their own transaction because XACT_ABORT correctly rolls back failed mutations.
DECLARE @U INT=(SELECT TOP(1) UserID FROM dbo.Users ORDER BY UserID),@I INT=(SELECT TOP(1) IngredientID FROM dbo.Ingredients),@Un INT=(SELECT TOP(1) UnitID FROM dbo.Units),@Op UNIQUEIDENTIFIER=NEWID(),@Batch INT;
IF @U IS NULL THROW 51000,N'Existing regression fixture required',1;
EXEC dbo.sp_AddInventoryBatch @U,@Op,@I,@Un,1;
BEGIN TRY
 EXEC dbo.sp_AddInventoryBatch @U,@Op,@I,@Un,2;
 THROW 51000,N'Operation ID payload reuse accepted',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>50004 THROW; END CATCH;
SELECT @Batch=MAX(BatchID) FROM dbo.InventoryBatches WHERE UserID=@U;
SET @Op=NEWID();
BEGIN TRY
 EXEC dbo.sp_AdjustInventoryBatch @U,@Op,@Batch,999,-1;
 THROW 51000,N'Stale version accepted',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>50004 THROW; END CATCH;
SET @Op=NEWID();
BEGIN TRY
 EXEC dbo.sp_AdjustInventoryBatch -1,@Op,@Batch,1,-1;
 THROW 51000,N'Foreign stock modified',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>50002 THROW; END CATCH;
PRINT N'PASS operation payload, stale-version and ownership rejection';
GO
SET XACT_ABORT ON;
BEGIN TRAN;
DECLARE @User INT,@Ingredient INT=(SELECT TOP(1) IngredientID FROM dbo.Ingredients),@G INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'g'),@Kg INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'kg'),@Piece INT=(SELECT TOP(1) UnitID FROM dbo.Units WHERE UnitType=N'count'),@ID INT,@Op UNIQUEIDENTIFIER;
INSERT dbo.Users(Username,Email,PasswordHash) VALUES(N'readiness-fixture',N'readiness@example.invalid',N'fixture'); SET @User=SCOPE_IDENTITY();
INSERT dbo.Recipes(UserID,Title,Instructions,Servings) VALUES(@User,N'ZZReadyFixture Ready',N'Cook',2); SET @ID=SCOPE_IDENTITY();
INSERT dbo.RecipeIngredients(RecipeID,IngredientID,UnitID,Quantity) VALUES(@ID,@Ingredient,@G,200);
INSERT dbo.Recipes(UserID,Title,Instructions,Servings) VALUES(@User,N'AAReadyFixture Missing',N'Cook',2); SET @ID=SCOPE_IDENTITY();
INSERT dbo.RecipeIngredients(RecipeID,IngredientID,UnitID,Quantity) VALUES(@ID,@Ingredient,@G,500);
SET @Op=NEWID(); EXEC dbo.sp_AddInventoryBatch @User,@Op,@Ingredient,@Kg,0.3,NULL,'2026-11-12';
SET @Op=NEWID(); EXEC dbo.sp_AddInventoryBatch @User,@Op,@Ingredient,@G,1000,NULL,'2026-11-10';
CREATE TABLE #Ready(RecipeID INT,Title NVARCHAR(150),UserID INT,AuthorUsername NVARCHAR(50),CategoryID INT,CategoryName NVARCHAR(50),PrepTimeMinutes INT,CookTimeMinutes INT,Servings INT,CreatedAt DATETIME2,PhotoData VARBINARY(MAX),ShortageCount INT,Readiness INT,TotalCount INT);
INSERT #Ready EXEC dbo.sp_BrowseReadyRecipes @User,'2026-11-11',2,N'ReadyFixture',NULL,1,1;
IF NOT EXISTS(SELECT 1 FROM #Ready WHERE Title=N'ZZReadyFixture Ready' AND Readiness=0 AND TotalCount=2) THROW 51000,N'Readiness must rank before pagination/title',1;
TRUNCATE TABLE #Ready;
INSERT #Ready EXEC dbo.sp_BrowseReadyRecipes @User,'2026-11-11',4,N'ReadyFixture',NULL,1,40;
IF EXISTS(SELECT 1 FROM #Ready WHERE Readiness<>1) THROW 51000,N'Servings or expired-stock exclusion incorrect',1;
SET @Op=NEWID(); EXEC dbo.sp_AddInventoryBatch @User,@Op,@Ingredient,@Piece,20;
TRUNCATE TABLE #Ready;
INSERT #Ready EXEC dbo.sp_BrowseReadyRecipes @User,'2026-11-11',4,N'ReadyFixture',NULL,1,40;
IF EXISTS(SELECT 1 FROM #Ready WHERE Readiness<>2) THROW 51000,N'Incompatible unit must require review',1;
ROLLBACK;
PRINT N'PASS readiness serving scaling, expiry, incompatible units and ranking before pagination';
GO
