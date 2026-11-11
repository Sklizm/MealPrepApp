USE MealPrepDB;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @U INT,@List INT,@I INT=(SELECT TOP(1) IngredientID FROM dbo.Ingredients),@Ml INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'ml'),@L INT=(SELECT UnitID FROM dbo.Units WHERE Abbreviation=N'l'),@Item INT,@Op UNIQUEIDENTIFIER=NEWID();
INSERT dbo.Users(Username,Email,PasswordHash) VALUES(N'shopping-batch-fixture',N'shopping-batch@example.invalid',N'fixture'); SET @U=SCOPE_IDENTITY();
INSERT dbo.ShoppingSnapshots(UserID,StartDate,EndDate) VALUES(@U,'2026-11-09','2026-11-15'); SET @List=SCOPE_IDENTITY();
EXEC dbo.sp_ChangeShoppingItem @U,@Op,@List,NULL,0,N'Free text one';
EXEC dbo.sp_ChangeShoppingItem @U,@Op,@List,NULL,0,N'Free text one';
SET @Op=NEWID(); EXEC dbo.sp_ChangeShoppingItem @U,@Op,@List,NULL,0,N'Free text two';
IF (SELECT COUNT(*) FROM dbo.ShoppingSnapshotItems WHERE SnapshotID=@List)<>2 THROW 51000,N'Manual groceries duplicated or disallowed',1;
SET @Op=NEWID(); EXEC dbo.sp_ChangeShoppingItem @U,@Op,@List,NULL,0,N'Milk fixture',@I,@Ml,200,1,1,@L,NULL,'2026-11-18';
SELECT @Item=MAX(ItemID) FROM dbo.ShoppingSnapshotItems WHERE SnapshotID=@List;
SET @Op=NEWID(); EXEC dbo.sp_ImportShoppingPurchase @U,@Op,@Item,1;
EXEC dbo.sp_ImportShoppingPurchase @U,@Op,@Item,1;
SET @Op=NEWID(); EXEC dbo.sp_ImportShoppingPurchase @U,@Op,@Item,1;
IF (SELECT SUM(RemainingQuantity) FROM dbo.InventoryBatches WHERE UserID=@U AND UnitID=@L)<>1 THROW 51000,N'Purchased litre was not imported exactly once',1;
IF NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshotItems WHERE ItemID=@Item AND ToBuyQty=200 AND PurchasedQuantity=1 AND Version=2 AND ImportedBatchID IS NOT NULL) THROW 51000,N'Required and purchased quantities or version changed',1;
SET @Op=NEWID();
BEGIN TRY
 EXEC dbo.sp_ChangeShoppingItem @U,@Op,@List,@Item,2,N'Milk fixture',@I,@Ml,200,1,2,@L,NULL,'2026-11-18';
 THROW 51000,N'Imported quantity silently overwritten',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>50004 THROW; END CATCH;
SELECT @Item=MIN(ItemID) FROM dbo.ShoppingSnapshotItems WHERE SnapshotID=@List;
SET @Op=NEWID(); EXEC dbo.sp_ChangeShoppingItem @U,@Op,@List,@Item,1,N'Free text one',NULL,NULL,1,1;
SET @Op=NEWID();
BEGIN TRY
 EXEC dbo.sp_ImportShoppingPurchase @U,@Op,@Item,2;
 THROW 51000,N'Unmapped grocery imported',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>50000 THROW; END CATCH;
SET @Op=NEWID();
BEGIN TRY
 EXEC dbo.sp_ChangeShoppingItem @U,@Op,@List,@Item,1,N'Free text one',NULL,NULL,1,0;
 THROW 51000,N'Stale purchase overwritten',1;
END TRY BEGIN CATCH IF ERROR_NUMBER()<>50004 THROW; END CATCH;
SET @Op=NEWID(); EXEC dbo.sp_ChangeShoppingItem @U,@Op,@List,@Item,2,N'Free text one',NULL,NULL,1,1,NULL,NULL,NULL,NULL,1;
IF NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshotItems WHERE ItemID=@Item AND IsDeleted=1 AND Version=3) THROW 51000,N'Deletion tombstone missing',1;
PRINT N'PASS manual groceries, purchased quantities, import idempotency, immutable imports, mapping, conflicts and tombstones';
GO
