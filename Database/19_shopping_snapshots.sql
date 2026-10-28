USE MealPrepDB;
GO
IF OBJECT_ID(N'dbo.ShoppingSnapshots',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ShoppingSnapshots (
 SnapshotID INT IDENTITY PRIMARY KEY, UserID INT NOT NULL REFERENCES dbo.Users(UserID),
 StartDate DATE NOT NULL, EndDate DATE NOT NULL, CreatedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_ShoppingSnapshot_Dates CHECK(EndDate>=StartDate));
 CREATE INDEX IX_ShoppingSnapshots_User ON dbo.ShoppingSnapshots(UserID,SnapshotID DESC);
 CREATE TABLE dbo.ShoppingSnapshotItems (
 ItemID INT IDENTITY PRIMARY KEY, SnapshotID INT NOT NULL REFERENCES dbo.ShoppingSnapshots(SnapshotID),
 IngredientID INT NOT NULL REFERENCES dbo.Ingredients(IngredientID), UnitID INT NOT NULL REFERENCES dbo.Units(UnitID),
 IngredientName NVARCHAR(150) NOT NULL, CategoryName NVARCHAR(100) NOT NULL, UnitAbbreviation NVARCHAR(20) NOT NULL,
 NeededQty DECIMAL(18,4) NOT NULL, OnHandQty DECIMAL(18,4) NOT NULL, ToBuyQty DECIMAL(10,2) NOT NULL,
 IsPurchased BIT NOT NULL DEFAULT 0, ImportedAt DATETIME2(0) NULL, UpdatedAt DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT UQ_ShoppingSnapshot_Item UNIQUE(SnapshotID,IngredientID,UnitID), CONSTRAINT CK_ShoppingSnapshot_Qty CHECK(ToBuyQty>0));
END;
GO
