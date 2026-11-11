USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ChangeShoppingItem
 @UserID INT,@OperationID UNIQUEIDENTIFIER,@SnapshotID INT,@ItemID INT=NULL,@ExpectedVersion BIGINT=0,
 @Name NVARCHAR(150),@IngredientID INT=NULL,@UnitID INT=NULL,@RequiredQuantity DECIMAL(18,4)=1,
 @IsPurchased BIT=0,@PurchasedQuantity DECIMAL(18,4)=NULL,@PurchasedUnitID INT=NULL,@PurchasedOn DATE=NULL,@ExpiresOn DATE=NULL,@IsDeleted BIT=0
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF NULLIF(LTRIM(RTRIM(@Name)),N'') IS NULL OR @RequiredQuantity<=0 OR @PurchasedQuantity<=0 OR (@PurchasedOn IS NOT NULL AND @ExpiresOn<@PurchasedOn) THROW 50000,N'Invalid shopping item',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'shopping-change' AS kind,@SnapshotID AS list,@ItemID AS id,@ExpectedVersion AS version,@Name AS name,@IngredientID AS ingredient,@UnitID AS unit,@RequiredQuantity AS required,@IsPurchased AS checked,@PurchasedQuantity AS purchased,@PurchasedUnitID AS purchasedUnit,@PurchasedOn AS purchasedOn,@ExpiresOn AS expiresOn,@IsDeleted AS deleted FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Version BIGINT,@Imported DATETIME2(0),@ID INT=@ItemID;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshots WITH(UPDLOCK,HOLDLOCK) WHERE SnapshotID=@SnapshotID AND UserID=@UserID) THROW 50002,N'Not authorized',1;
  IF @ItemID IS NULL
  BEGIN
   IF @ExpectedVersion<>0 OR @IsDeleted=1 THROW 50004,N'Invalid new item version',1;
   INSERT dbo.ShoppingSnapshotItems(SnapshotID,IngredientID,UnitID,IngredientName,CategoryName,UnitAbbreviation,NeededQty,OnHandQty,ToBuyQty,IsPurchased,PurchasedQuantity,PurchasedUnitID,PurchasedOn,ExpiresOn,IsManual)
   VALUES(@SnapshotID,@IngredientID,@UnitID,@Name,N'Manual',COALESCE((SELECT Abbreviation FROM dbo.Units WHERE UnitID=@UnitID),N''),@RequiredQuantity,0,@RequiredQuantity,@IsPurchased,@PurchasedQuantity,@PurchasedUnitID,@PurchasedOn,@ExpiresOn,1);
   SET @ID=SCOPE_IDENTITY(); SET @Version=1;
  END
  ELSE
  BEGIN
   SELECT @Version=Version,@Imported=ImportedAt FROM dbo.ShoppingSnapshotItems WITH(UPDLOCK,HOLDLOCK) WHERE ItemID=@ItemID AND SnapshotID=@SnapshotID AND IsDeleted=0;
   IF @Version IS NULL OR @Version<>@ExpectedVersion THROW 50004,N'Shopping item changed',1;
   IF @Imported IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.ShoppingSnapshotItems WHERE ItemID=@ItemID AND
    (ISNULL(IngredientID,-1)<>ISNULL(@IngredientID,-1) OR ISNULL(UnitID,-1)<>ISNULL(@UnitID,-1) OR ISNULL(PurchasedQuantity,-1)<>ISNULL(@PurchasedQuantity,-1) OR ISNULL(PurchasedUnitID,-1)<>ISNULL(@PurchasedUnitID,-1) OR ISNULL(PurchasedOn,'00010101')<>ISNULL(@PurchasedOn,'00010101') OR ISNULL(ExpiresOn,'00010101')<>ISNULL(@ExpiresOn,'00010101')))
    THROW 50004,N'Imported purchases require an explicit stock correction',1;
   UPDATE dbo.ShoppingSnapshotItems SET IngredientName=@Name,IngredientID=@IngredientID,UnitID=@UnitID,UnitAbbreviation=COALESCE((SELECT Abbreviation FROM dbo.Units WHERE UnitID=@UnitID),N''),
    IsPurchased=@IsPurchased,PurchasedQuantity=@PurchasedQuantity,PurchasedUnitID=@PurchasedUnitID,PurchasedOn=@PurchasedOn,ExpiresOn=@ExpiresOn,IsDeleted=@IsDeleted
   WHERE ItemID=@ItemID;
   SET @Version=@Version+1;
  END;
  EXEC dbo.sp_WriteAudit @UserID,N'SHOPPING_CHANGE',N'ShoppingItem',@ID;
  SET @Result=(SELECT @ID AS id,@Version AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ImportShoppingPurchase @UserID INT,@OperationID UNIQUEIDENTIFIER,@ItemID INT,@ExpectedVersion BIGINT
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'shopping-import' AS kind,@ItemID AS id,@ExpectedVersion AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Ingredient INT,@Unit INT,@Qty DECIMAL(18,4),@PurchasedOn DATE,@Expiry DATE,@Version BIGINT,@Imported DATETIME2(0),@Batch INT,@Purchased BIT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Ingredient=i.IngredientID,@Unit=COALESCE(i.PurchasedUnitID,i.UnitID),@Qty=COALESCE(i.PurchasedQuantity,i.ToBuyQty),@PurchasedOn=i.PurchasedOn,@Expiry=i.ExpiresOn,@Version=i.Version,@Imported=i.ImportedAt,@Batch=i.ImportedBatchID,@Purchased=i.IsPurchased
  FROM dbo.ShoppingSnapshotItems i WITH(UPDLOCK,HOLDLOCK) JOIN dbo.ShoppingSnapshots s ON s.SnapshotID=i.SnapshotID
  WHERE i.ItemID=@ItemID AND s.UserID=@UserID AND i.IsDeleted=0;
  IF @Version IS NULL THROW 50002,N'Purchased item not available',1;
  IF @Purchased=0 THROW 50000,N'Item must be purchased before import',1;
  IF @Imported IS NULL
  BEGIN
   IF @Version<>@ExpectedVersion THROW 50004,N'Shopping item changed; review purchase again',1;
   IF @Ingredient IS NULL OR @Unit IS NULL OR @Qty<=0 THROW 50000,N'Map an ingredient and unit before importing',1;
   INSERT dbo.InventoryBatches(UserID,IngredientID,UnitID,RemainingQuantity,PurchasedOn,ExpiresOn) VALUES(@UserID,@Ingredient,@Unit,@Qty,@PurchasedOn,@Expiry);
   SET @Batch=SCOPE_IDENTITY();
   INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter) VALUES(@Batch,@UserID,@Qty,N'Purchase',1);
   EXEC dbo.sp_RefreshPantryTotal @UserID,@Ingredient,@Unit;
   UPDATE dbo.ShoppingSnapshotItems SET ImportedAt=SYSUTCDATETIME(),ImportedBatchID=@Batch WHERE ItemID=@ItemID;
   SET @Version=@Version+1;
   EXEC dbo.sp_WriteAudit @UserID,N'SHOPPING_IMPORT',N'ShoppingItem',@ItemID;
  END;
  SET @Result=(SELECT @ItemID AS id,@Version AS version,@Batch AS batchId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
-- Legacy clients still import explicitly and idempotently; actual purchase quantities
-- take precedence when a newer client recorded them.
CREATE OR ALTER PROCEDURE dbo.sp_ImportShoppingItem @UserID INT,@ItemID INT
AS BEGIN
 SET NOCOUNT ON;
 DECLARE @Version BIGINT,@Op UNIQUEIDENTIFIER=NEWID();
 SELECT @Version=i.Version FROM dbo.ShoppingSnapshotItems i JOIN dbo.ShoppingSnapshots s ON s.SnapshotID=i.SnapshotID WHERE s.UserID=@UserID AND i.ItemID=@ItemID;
 EXEC dbo.sp_ImportShoppingPurchase @UserID,@Op,@ItemID,@Version;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetShoppingSnapshotItems @UserID INT,@SnapshotID INT
AS BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS(SELECT 1 FROM dbo.ShoppingSnapshots WHERE SnapshotID=@SnapshotID AND UserID=@UserID) THROW 50002,N'Not authorized',1;
 SELECT i.*,COALESCE(pu.Abbreviation,i.UnitAbbreviation) AS PurchasedUnitAbbreviation,CONVERT(BIT,CASE WHEN ImportedAt IS NULL THEN 0 ELSE 1 END) AS IsImported
 FROM dbo.ShoppingSnapshotItems i LEFT JOIN dbo.Units pu ON pu.UnitID=i.PurchasedUnitID WHERE SnapshotID=@SnapshotID AND IsDeleted=0 ORDER BY CategoryName,IngredientName,ItemID;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetShoppingChanges @UserID INT AS BEGIN
 SET NOCOUNT ON;
 SELECT i.*,COALESCE(pu.Abbreviation,i.UnitAbbreviation) AS PurchasedUnitAbbreviation,CONVERT(BIT,CASE WHEN ImportedAt IS NULL THEN 0 ELSE 1 END) AS IsImported
 FROM dbo.ShoppingSnapshotItems i JOIN dbo.ShoppingSnapshots s ON s.SnapshotID=i.SnapshotID LEFT JOIN dbo.Units pu ON pu.UnitID=i.PurchasedUnitID
 WHERE s.UserID=@UserID ORDER BY i.ItemID;
END;
GO
