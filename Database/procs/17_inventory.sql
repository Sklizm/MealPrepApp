USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- Every operation locks a per-user key inside its caller's transaction. A repeated
-- operation returns the original result; reusing an ID for a different payload fails.
CREATE OR ALTER PROCEDURE dbo.sp_BeginClientOperation @UserID INT,@OperationID UNIQUEIDENTIFIER,@Request NVARCHAR(MAX),@Result NVARCHAR(MAX) OUTPUT
AS BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT=0 OR @OperationID IS NULL THROW 50000,N'Operation requires a transaction and ID',1;
 DECLARE @Resource NVARCHAR(255)=CONCAT(N'mealprep-user-',@UserID),@LockResult INT;
 EXEC @LockResult=sys.sp_getapplock @Resource=@Resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000;
 IF @LockResult<0 THROW 50004,N'Concurrent operation; retry',1;
 SET @Result=NULL;
 DECLARE @Hash BINARY(32);
 SELECT @Hash=RequestHash,@Result=ResultJson FROM dbo.ClientOperations WHERE UserID=@UserID AND OperationID=@OperationID;
 IF @Hash IS NOT NULL AND @Hash<>HASHBYTES('SHA2_256',@Request) THROW 50004,N'Operation ID was reused with different data',1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_CompleteClientOperation @UserID INT,@OperationID UNIQUEIDENTIFIER,@Request NVARCHAR(MAX),@Result NVARCHAR(MAX)
AS BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 50000,N'Operation requires a transaction',1;
 INSERT dbo.ClientOperations(UserID,OperationID,RequestHash,ResultJson) VALUES(@UserID,@OperationID,HASHBYTES('SHA2_256',@Request),@Result);
END;
GO
-- Internal total refresh; ownership is enforced by every public caller.
CREATE OR ALTER PROCEDURE dbo.sp_RefreshPantryTotal @UserID INT,@IngredientID INT,@UnitID INT
AS BEGIN
 SET NOCOUNT ON;
 DECLARE @Total DECIMAL(18,4);
 SELECT @Total=COALESCE(SUM(RemainingQuantity),0) FROM dbo.InventoryBatches WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@UserID AND IngredientID=@IngredientID AND UnitID=@UnitID;
 IF @Total=0 DELETE dbo.UserPantry WHERE UserID=@UserID AND IngredientID=@IngredientID AND UnitID=@UnitID;
 ELSE
 BEGIN
  UPDATE dbo.UserPantry WITH(UPDLOCK,HOLDLOCK) SET Quantity=@Total,UpdatedAt=SYSUTCDATETIME() WHERE UserID=@UserID AND IngredientID=@IngredientID AND UnitID=@UnitID;
  IF @@ROWCOUNT=0 INSERT dbo.UserPantry(UserID,IngredientID,UnitID,Quantity) VALUES(@UserID,@IngredientID,@UnitID,@Total);
 END;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetInventoryBatches @UserID INT,@IncludeEmpty BIT=0
AS BEGIN
 SET NOCOUNT ON;
 SELECT b.*,i.Name AS IngredientName,u.Abbreviation AS UnitAbbreviation,COALESCE(c.Name,N'Fără categorie') AS CategoryName
 FROM dbo.InventoryBatches b JOIN dbo.Ingredients i ON i.IngredientID=b.IngredientID JOIN dbo.Units u ON u.UnitID=b.UnitID
 LEFT JOIN dbo.IngredientCategories c ON c.IngredientCategoryID=i.IngredientCategoryID
 WHERE b.UserID=@UserID AND (@IncludeEmpty=1 OR b.RemainingQuantity>0)
 ORDER BY i.Name,CASE WHEN b.ExpiresOn IS NULL THEN 1 ELSE 0 END,b.ExpiresOn,b.BatchID;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AddInventoryBatch @UserID INT,@OperationID UNIQUEIDENTIFIER,@IngredientID INT,@UnitID INT,@Quantity DECIMAL(18,4),@PurchasedOn DATE=NULL,@ExpiresOn DATE=NULL
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Quantity IS NULL OR @Quantity<=0 THROW 50000,N'Positive quantity required',1;
 IF @PurchasedOn IS NOT NULL AND @ExpiresOn<@PurchasedOn THROW 50000,N'Expiry precedes purchase',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'add-stock' AS kind,@IngredientID AS ingredient,@UnitID AS unit,@Quantity AS quantity,@PurchasedOn AS purchasedOn,@ExpiresOn AS expiresOn FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@ID INT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  INSERT dbo.InventoryBatches(UserID,IngredientID,UnitID,RemainingQuantity,PurchasedOn,ExpiresOn) VALUES(@UserID,@IngredientID,@UnitID,@Quantity,@PurchasedOn,@ExpiresOn);
  SET @ID=SCOPE_IDENTITY();
  INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter) VALUES(@ID,@UserID,@Quantity,N'Purchase',1);
  EXEC dbo.sp_RefreshPantryTotal @UserID,@IngredientID,@UnitID;
  EXEC dbo.sp_WriteAudit @UserID,N'STOCK_PURCHASE',N'InventoryBatch',@ID;
  SET @Result=(SELECT @ID AS id,CONVERT(BIGINT,1) AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AdjustInventoryBatch @UserID INT,@OperationID UNIQUEIDENTIFIER,@BatchID INT,@ExpectedVersion BIGINT,@Delta DECIMAL(18,4),@Kind NVARCHAR(30)=N'Correction'
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Delta IS NULL OR @Delta=0 OR @Kind NOT IN(N'Correction',N'Discard') OR (@Kind=N'Discard' AND @Delta>0) THROW 50000,N'Invalid adjustment',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'adjust-stock' AS kind,@BatchID AS id,@ExpectedVersion AS version,@Delta AS delta,@Kind AS reason FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Ingredient INT,@Unit INT,@Event INT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Ingredient=IngredientID,@Unit=UnitID FROM dbo.InventoryBatches WITH(UPDLOCK,HOLDLOCK) WHERE BatchID=@BatchID AND UserID=@UserID;
  IF @Ingredient IS NULL THROW 50002,N'Not authorized',1;
  UPDATE dbo.InventoryBatches SET RemainingQuantity=RemainingQuantity+@Delta,Version=Version+1,UpdatedAt=SYSUTCDATETIME() WHERE BatchID=@BatchID AND Version=@ExpectedVersion AND RemainingQuantity+@Delta>=0;
  IF @@ROWCOUNT=0 THROW 50004,N'Stock changed or insufficient quantity',1;
  INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter) VALUES(@BatchID,@UserID,@Delta,@Kind,@ExpectedVersion+1);
  SET @Event=SCOPE_IDENTITY();
  EXEC dbo.sp_RefreshPantryTotal @UserID,@Ingredient,@Unit;
  EXEC dbo.sp_WriteAudit @UserID,N'STOCK_ADJUST',N'StockEvent',@Event;
  SET @Result=(SELECT @BatchID AS id,@ExpectedVersion+1 AS version,@Event AS eventId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_UndoStockEvent @UserID INT,@OperationID UNIQUEIDENTIFIER,@EventID INT
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'undo-stock' AS kind,@EventID AS id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Batch INT,@Ingredient INT,@Unit INT,@Delta DECIMAL(18,4),@Version BIGINT,@Undo INT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Batch=e.BatchID,@Delta=e.QuantityDelta,@Version=e.VersionAfter,@Ingredient=b.IngredientID,@Unit=b.UnitID
  FROM dbo.StockEvents e JOIN dbo.InventoryBatches b WITH(UPDLOCK,HOLDLOCK) ON b.BatchID=e.BatchID
  WHERE e.EventID=@EventID AND e.UserID=@UserID AND e.Kind=N'Correction';
  IF @Batch IS NULL THROW 50002,N'Event cannot be undone',1;
  IF EXISTS(SELECT 1 FROM dbo.StockEvents WHERE UndoOfEventID=@EventID) THROW 50004,N'Already undone',1;
  UPDATE dbo.InventoryBatches SET RemainingQuantity=RemainingQuantity-@Delta,Version=Version+1,UpdatedAt=SYSUTCDATETIME() WHERE BatchID=@Batch AND Version=@Version AND RemainingQuantity-@Delta>=0;
  IF @@ROWCOUNT=0 THROW 50004,N'Subsequent use prevents undo',1;
  INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter,UndoOfEventID) VALUES(@Batch,@UserID,-@Delta,N'Undo',@Version+1,@EventID);
  SET @Undo=SCOPE_IDENTITY();
  EXEC dbo.sp_RefreshPantryTotal @UserID,@Ingredient,@Unit;
  EXEC dbo.sp_WriteAudit @UserID,N'STOCK_UNDO',N'StockEvent',@Undo;
  SET @Result=(SELECT @Batch AS id,@Version+1 AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetUnitMap AS BEGIN SET NOCOUNT ON; SELECT * FROM dbo.fn_ShoppingUnitMap(); END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_BrowseReadyRecipes @UserID INT,@Today DATE,@Servings DECIMAL(18,4)=NULL,@Term NVARCHAR(150)=N'',@CategoryID INT=NULL,@PageNumber INT=1,@PageSize INT=40
AS BEGIN
 SET NOCOUNT ON;
 IF @Today IS NULL OR @PageNumber<1 OR @PageSize<1 OR @PageSize>200 OR (@Servings IS NOT NULL AND (@Servings<=0 OR @Servings>1000)) THROW 50000,N'Invalid readiness request',1;
 ;WITH requirements AS(
  SELECT ri.RecipeID,ri.IngredientID,m.CanonicalUnitID,
   SUM(ri.Quantity*m.Multiplier*COALESCE(@Servings,r.Servings,1)/COALESCE(NULLIF(r.Servings,0),1)) AS RequiredQty
  FROM dbo.RecipeIngredients ri JOIN dbo.Recipes r ON r.RecipeID=ri.RecipeID JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=ri.UnitID
  GROUP BY ri.RecipeID,ri.IngredientID,m.CanonicalUnitID
 ),stock AS(
  SELECT b.IngredientID,m.CanonicalUnitID,SUM(b.RemainingQuantity*m.Multiplier) AS Qty
  FROM dbo.InventoryBatches b JOIN dbo.fn_ShoppingUnitMap() m ON m.UnitID=b.UnitID
  WHERE b.UserID=@UserID AND b.RemainingQuantity>0 AND (b.ExpiresOn IS NULL OR b.ExpiresOn>=@Today)
  GROUP BY b.IngredientID,m.CanonicalUnitID
 ),shortages AS(
  SELECT q.RecipeID,COUNT(*) AS IngredientCount,
   SUM(CASE WHEN q.RequiredQty>COALESCE(s.Qty,0) THEN 1 ELSE 0 END) AS ShortageCount,
   MAX(CASE WHEN q.RequiredQty>COALESCE(s.Qty,0) AND other.HasOther=1 THEN 1 ELSE 0 END) AS Review
  FROM requirements q LEFT JOIN stock s ON s.IngredientID=q.IngredientID AND s.CanonicalUnitID=q.CanonicalUnitID
  OUTER APPLY(SELECT TOP(1) 1 AS HasOther FROM stock o WHERE o.IngredientID=q.IngredientID AND o.CanonicalUnitID<>q.CanonicalUnitID) other
  GROUP BY q.RecipeID
 ),ranked AS(
  SELECT r.RecipeID,r.Title,r.UserID,u.Username AS AuthorUsername,r.CategoryID,c.Name AS CategoryName,r.PrepTimeMinutes,r.CookTimeMinutes,r.Servings,r.CreatedAt,
   COALESCE(p.ThumbnailData,p.ImageData) AS PhotoData,COALESCE(s.ShortageCount,0) AS ShortageCount,
   CASE WHEN r.Servings IS NULL OR r.Servings<=0 OR s.IngredientCount IS NULL OR s.Review=1 THEN 2 WHEN s.ShortageCount>0 THEN 1 ELSE 0 END AS Readiness
  FROM dbo.Recipes r JOIN dbo.Users u ON u.UserID=r.UserID LEFT JOIN dbo.Categories c ON c.CategoryID=r.CategoryID
  LEFT JOIN dbo.RecipePhotos p ON p.RecipeID=r.RecipeID LEFT JOIN shortages s ON s.RecipeID=r.RecipeID
  WHERE (@CategoryID IS NULL OR r.CategoryID=@CategoryID) AND (@Term=N'' OR CHARINDEX(@Term,r.Title)>0)
 )
 SELECT *,COUNT(*) OVER() AS TotalCount FROM ranked ORDER BY Readiness,Title,RecipeID
 OFFSET (@PageNumber-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GetStockThresholds @UserID INT AS BEGIN SET NOCOUNT ON; SELECT IngredientID,UnitID,Quantity,Version FROM dbo.StockThresholds WHERE UserID=@UserID; END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_SetStockThreshold @UserID INT,@OperationID UNIQUEIDENTIFIER,@IngredientID INT,@UnitID INT,@Quantity DECIMAL(18,4),@ExpectedVersion BIGINT
AS BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Quantity<0 THROW 50000,N'Invalid threshold',1;
 DECLARE @Request NVARCHAR(MAX)=(SELECT N'threshold' AS kind,@IngredientID AS ingredient,@UnitID AS unit,@Quantity AS quantity,@ExpectedVersion AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@Result NVARCHAR(MAX),@Current BIGINT;
 BEGIN TRY BEGIN TRAN;
 EXEC dbo.sp_BeginClientOperation @UserID,@OperationID,@Request,@Result OUTPUT;
 IF @Result IS NULL
 BEGIN
  SELECT @Current=Version FROM dbo.StockThresholds WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@UserID AND IngredientID=@IngredientID AND UnitID=@UnitID;
  IF COALESCE(@Current,0)<>@ExpectedVersion THROW 50004,N'Threshold changed',1;
  IF @Current IS NULL INSERT dbo.StockThresholds(UserID,IngredientID,UnitID,Quantity) VALUES(@UserID,@IngredientID,@UnitID,@Quantity);
  ELSE UPDATE dbo.StockThresholds SET Quantity=@Quantity,Version=Version+1,UpdatedAt=SYSUTCDATETIME() WHERE UserID=@UserID AND IngredientID=@IngredientID AND UnitID=@UnitID;
  EXEC dbo.sp_WriteAudit @UserID,N'STOCK_THRESHOLD',N'Ingredient',@IngredientID;
  SET @Result=(SELECT @IngredientID AS id,COALESCE(@Current,0)+1 AS version FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
  EXEC dbo.sp_CompleteClientOperation @UserID,@OperationID,@Request,@Result;
 END;
 COMMIT; SELECT @Result;
 END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
