USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- Additive inventory ledger. Calendar dates are never inferred from UTC timestamps.
IF OBJECT_ID(N'dbo.InventoryBatches',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.InventoryBatches(
  BatchID INT IDENTITY PRIMARY KEY, UserID INT NOT NULL REFERENCES dbo.Users(UserID),
  IngredientID INT NOT NULL REFERENCES dbo.Ingredients(IngredientID), UnitID INT NOT NULL REFERENCES dbo.Units(UnitID),
  RemainingQuantity DECIMAL(18,4) NOT NULL CHECK(RemainingQuantity>=0),
  PurchasedOn DATE NULL, ExpiresOn DATE NULL, SourceAddedAt DATETIME2(0) NULL,
  CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
  Version BIGINT NOT NULL DEFAULT 1,
  CHECK(PurchasedOn IS NULL OR ExpiresOn IS NULL OR ExpiresOn>=PurchasedOn));
 CREATE INDEX IX_InventoryBatches_Stock ON dbo.InventoryBatches(UserID,IngredientID,UnitID,ExpiresOn) INCLUDE(RemainingQuantity,Version);
 CREATE TABLE dbo.StockEvents(
  EventID INT IDENTITY PRIMARY KEY, BatchID INT NOT NULL REFERENCES dbo.InventoryBatches(BatchID),
  UserID INT NOT NULL REFERENCES dbo.Users(UserID), QuantityDelta DECIMAL(18,4) NOT NULL,
  Kind NVARCHAR(30) NOT NULL, VersionAfter BIGINT NOT NULL, UndoOfEventID INT NULL REFERENCES dbo.StockEvents(EventID),
  CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME());
 CREATE UNIQUE INDEX UX_StockEvents_Undo ON dbo.StockEvents(UndoOfEventID) WHERE UndoOfEventID IS NOT NULL;
 CREATE TABLE dbo.StockThresholds(
  UserID INT NOT NULL REFERENCES dbo.Users(UserID), IngredientID INT NOT NULL REFERENCES dbo.Ingredients(IngredientID),
  UnitID INT NOT NULL REFERENCES dbo.Units(UnitID), Quantity DECIMAL(18,4) NOT NULL CHECK(Quantity>=0),
  Version BIGINT NOT NULL DEFAULT 1, UpdatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
  PRIMARY KEY(UserID,IngredientID,UnitID));
END;
GO
-- Retain the legacy total table and interfaces; widen storage to avoid rounding batch quantities.
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.UserPantry') AND name=N'Quantity' AND scale=2)
 ALTER TABLE dbo.UserPantry ALTER COLUMN Quantity DECIMAL(18,4) NOT NULL;
GO
-- One undated batch per pre-existing total, once only. Known timestamps remain timestamps.
IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=1 AND major_id=OBJECT_ID(N'dbo.InventoryBatches') AND name=N'LegacyStockMigrated')
BEGIN
 SET XACT_ABORT ON;
 BEGIN TRAN;
 INSERT dbo.InventoryBatches(UserID,IngredientID,UnitID,RemainingQuantity,SourceAddedAt,CreatedAt,UpdatedAt)
 SELECT UserID,IngredientID,UnitID,Quantity,AddedAt,AddedAt,COALESCE(UpdatedAt,AddedAt) FROM dbo.UserPantry WITH(TABLOCKX);
 INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter)
 SELECT BatchID,UserID,RemainingQuantity,N'Migrated',Version FROM dbo.InventoryBatches;
 EXEC sys.sp_addextendedproperty @name=N'LegacyStockMigrated',@value=1,@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'InventoryBatches';
 COMMIT;
END;
GO
IF OBJECT_ID(N'dbo.ClientOperations',N'U') IS NULL
 CREATE TABLE dbo.ClientOperations(
  UserID INT NOT NULL REFERENCES dbo.Users(UserID), OperationID UNIQUEIDENTIFIER NOT NULL,
  RequestHash BINARY(32) NOT NULL, ResultJson NVARCHAR(MAX) NOT NULL CHECK(ISJSON(ResultJson)=1),
  CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(), PRIMARY KEY(UserID,OperationID));
GO
-- Legacy writes become audited corrections. New batch procedures update batches first;
-- their matching totals cause no correction. No session flags can suppress this invariant.
CREATE OR ALTER TRIGGER dbo.tr_UserPantry_Batches ON dbo.UserPantry AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @User INT,@Ingredient INT,@Unit INT,@Desired DECIMAL(18,4),@Actual DECIMAL(18,4),@Delta DECIMAL(18,4),@Batch INT,@Qty DECIMAL(18,4),@Take DECIMAL(18,4);
 DECLARE totals CURSOR LOCAL FAST_FORWARD FOR
 SELECT k.UserID,k.IngredientID,k.UnitID,COALESCE(i.Quantity,0)
 FROM(SELECT UserID,IngredientID,UnitID FROM inserted UNION SELECT UserID,IngredientID,UnitID FROM deleted) k
 LEFT JOIN inserted i ON i.UserID=k.UserID AND i.IngredientID=k.IngredientID AND i.UnitID=k.UnitID;
 OPEN totals; FETCH NEXT FROM totals INTO @User,@Ingredient,@Unit,@Desired;
 WHILE @@FETCH_STATUS=0
 BEGIN
  SELECT @Actual=COALESCE(SUM(RemainingQuantity),0) FROM dbo.InventoryBatches WITH(UPDLOCK,HOLDLOCK) WHERE UserID=@User AND IngredientID=@Ingredient AND UnitID=@Unit;
  SET @Delta=@Desired-@Actual;
  IF @Delta>0
  BEGIN
   INSERT dbo.InventoryBatches(UserID,IngredientID,UnitID,RemainingQuantity) VALUES(@User,@Ingredient,@Unit,@Delta);
   SET @Batch=SCOPE_IDENTITY();
   INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter) VALUES(@Batch,@User,@Delta,N'LegacyCorrection',1);
  END;
  IF @Delta<0
  BEGIN
   DECLARE batches CURSOR LOCAL FAST_FORWARD FOR SELECT BatchID,RemainingQuantity FROM dbo.InventoryBatches WHERE UserID=@User AND IngredientID=@Ingredient AND UnitID=@Unit AND RemainingQuantity>0 ORDER BY CASE WHEN ExpiresOn IS NULL THEN 1 ELSE 0 END,ExpiresOn,BatchID;
   OPEN batches; FETCH NEXT FROM batches INTO @Batch,@Qty;
   WHILE @@FETCH_STATUS=0 AND @Delta<0
   BEGIN
    SET @Take=CASE WHEN @Qty< -@Delta THEN @Qty ELSE -@Delta END;
    UPDATE dbo.InventoryBatches SET RemainingQuantity=RemainingQuantity-@Take,Version=Version+1,UpdatedAt=SYSUTCDATETIME() WHERE BatchID=@Batch;
    INSERT dbo.StockEvents(BatchID,UserID,QuantityDelta,Kind,VersionAfter) SELECT @Batch,@User,-@Take,N'LegacyCorrection',Version FROM dbo.InventoryBatches WHERE BatchID=@Batch;
    SET @Delta=@Delta+@Take;
    FETCH NEXT FROM batches INTO @Batch,@Qty;
   END;
   CLOSE batches; DEALLOCATE batches;
  END;
  FETCH NEXT FROM totals INTO @User,@Ingredient,@Unit,@Desired;
 END;
 CLOSE totals; DEALLOCATE totals;
END;
GO
