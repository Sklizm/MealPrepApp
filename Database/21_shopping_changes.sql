USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH(N'dbo.ShoppingSnapshotItems',N'Version') IS NULL
BEGIN
 ALTER TABLE dbo.ShoppingSnapshotItems ADD
  Version BIGINT NOT NULL CONSTRAINT DF_ShoppingItem_Version DEFAULT 1,
  PurchasedQuantity DECIMAL(18,4) NULL,
  PurchasedUnitID INT NULL REFERENCES dbo.Units(UnitID),
  PurchasedOn DATE NULL, ExpiresOn DATE NULL,
  IsManual BIT NOT NULL CONSTRAINT DF_ShoppingItem_Manual DEFAULT 0,
  IsDeleted BIT NOT NULL CONSTRAINT DF_ShoppingItem_Deleted DEFAULT 0,
  ImportedBatchID INT NULL CONSTRAINT FK_ShoppingSnapshotItems_InventoryBatch REFERENCES dbo.InventoryBatches(BatchID);
 ALTER TABLE dbo.ShoppingSnapshotItems ALTER COLUMN ToBuyQty DECIMAL(18,4) NOT NULL;
 ALTER TABLE dbo.ShoppingSnapshotItems DROP CONSTRAINT UQ_ShoppingSnapshot_Item;
 ALTER TABLE dbo.ShoppingSnapshotItems ALTER COLUMN IngredientID INT NULL;
 ALTER TABLE dbo.ShoppingSnapshotItems ALTER COLUMN UnitID INT NULL;

END;
GO
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_ShoppingItem_PurchasedQty')
 ALTER TABLE dbo.ShoppingSnapshotItems ADD CONSTRAINT CK_ShoppingItem_PurchasedQty CHECK(PurchasedQuantity IS NULL OR PurchasedQuantity>0);
GO
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_ShoppingSnapshotItems_InventoryBatch')
 ALTER TABLE dbo.ShoppingSnapshotItems ADD CONSTRAINT FK_ShoppingSnapshotItems_InventoryBatch FOREIGN KEY(ImportedBatchID) REFERENCES dbo.InventoryBatches(BatchID);
GO
CREATE OR ALTER TRIGGER dbo.tr_ShoppingItem_Version ON dbo.ShoppingSnapshotItems AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF TRIGGER_NESTLEVEL(OBJECT_ID(N'dbo.tr_ShoppingItem_Version'))>1 RETURN;
 UPDATE target SET Version=old.Version+1,UpdatedAt=SYSUTCDATETIME()
 FROM dbo.ShoppingSnapshotItems target JOIN inserted i ON i.ItemID=target.ItemID JOIN deleted old ON old.ItemID=target.ItemID;
END;
GO
