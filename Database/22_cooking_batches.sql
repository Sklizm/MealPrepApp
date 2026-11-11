USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF OBJECT_ID(N'dbo.CookingBatches',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.CookingBatches(
  CookingBatchID INT IDENTITY PRIMARY KEY,UserID INT NOT NULL REFERENCES dbo.Users(UserID),
  RecipeID INT NULL REFERENCES dbo.Recipes(RecipeID) ON DELETE SET NULL,
  PlannedPortions DECIMAL(18,4) NOT NULL CHECK(PlannedPortions>0),ProducedPortions DECIMAL(18,4) NULL CHECK(ProducedPortions>0),
  UsedPortions DECIMAL(18,4) NOT NULL DEFAULT 0 CHECK(UsedPortions>=0),
  RecipeSnapshot NVARCHAR(MAX) NULL CHECK(RecipeSnapshot IS NULL OR ISJSON(RecipeSnapshot)=1),
  NutritionSnapshot NVARCHAR(MAX) NULL CHECK(NutritionSnapshot IS NULL OR ISJSON(NutritionSnapshot)=1),
  CookedAt DATETIME2(7) NULL,PreparedOn DATE NULL,ExpiresOn DATE NULL,ResizeWithMeal BIT NOT NULL DEFAULT 0,
  CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),Version BIGINT NOT NULL DEFAULT 1,
  CHECK(UsedPortions<=COALESCE(ProducedPortions,PlannedPortions)),CHECK(ExpiresOn IS NULL OR PreparedOn IS NULL OR ExpiresOn>=PreparedOn));
 CREATE INDEX IX_CookingBatches_User ON dbo.CookingBatches(UserID,CookingBatchID);
 CREATE TABLE dbo.PortionEvents(
  PortionEventID INT IDENTITY PRIMARY KEY,CookingBatchID INT NOT NULL REFERENCES dbo.CookingBatches(CookingBatchID),
  UserID INT NOT NULL REFERENCES dbo.Users(UserID),MealPlanEntryID INT NULL REFERENCES dbo.MealPlanEntries(MealPlanEntryID) ON DELETE SET NULL,
  Kind NVARCHAR(20) NOT NULL CHECK(Kind IN(N'Consumed',N'Discarded')),Portions DECIMAL(18,4) NOT NULL CHECK(Portions>0),
  CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME());
 CREATE TABLE dbo.WeekTemplates(
  TemplateID INT IDENTITY PRIMARY KEY,UserID INT NOT NULL REFERENCES dbo.Users(UserID),Name NVARCHAR(100) NOT NULL,
  EntriesJson NVARCHAR(MAX) NOT NULL CHECK(ISJSON(EntriesJson)=1),Version BIGINT NOT NULL DEFAULT 1,
  CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME());
END;
GO
IF COL_LENGTH(N'dbo.MealPlanEntries',N'CookingBatchID') IS NULL
 ALTER TABLE dbo.MealPlanEntries ADD CookingBatchID INT NULL REFERENCES dbo.CookingBatches(CookingBatchID),
 PortionQuantity DECIMAL(18,4) NULL,ConsumedPortions DECIMAL(18,4) NOT NULL DEFAULT 0,Version BIGINT NOT NULL DEFAULT 1;
GO
IF COL_LENGTH(N'dbo.StockEvents',N'CookingBatchID') IS NULL
 ALTER TABLE dbo.StockEvents ADD CookingBatchID INT NULL REFERENCES dbo.CookingBatches(CookingBatchID);
GO
-- Also invoked by legacy planning/copy operations. Existing plans become independent
-- planned batches only: no cooked, consumed or discarded history is invented.
CREATE OR ALTER PROCEDURE dbo.sp_EnsurePlannedBatches @UserID INT=NULL
AS BEGIN
 SET NOCOUNT ON;
 DECLARE @Entry INT,@Owner INT,@Recipe INT,@Portions DECIMAL(18,4),@Batch INT;
 DECLARE entries CURSOR LOCAL FAST_FORWARD FOR
 SELECT p.MealPlanEntryID,p.UserID,p.RecipeID,COALESCE(p.PortionQuantity,p.Servings,r.Servings,1)
 FROM dbo.MealPlanEntries p WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Recipes r ON r.RecipeID=p.RecipeID
 WHERE p.CookingBatchID IS NULL AND (@UserID IS NULL OR p.UserID=@UserID);
 OPEN entries; FETCH NEXT FROM entries INTO @Entry,@Owner,@Recipe,@Portions;
 WHILE @@FETCH_STATUS=0
 BEGIN
  INSERT dbo.CookingBatches(UserID,RecipeID,PlannedPortions,ResizeWithMeal) VALUES(@Owner,@Recipe,@Portions,1); SET @Batch=SCOPE_IDENTITY();
  UPDATE dbo.MealPlanEntries SET CookingBatchID=@Batch,PortionQuantity=@Portions WHERE MealPlanEntryID=@Entry;
  FETCH NEXT FROM entries INTO @Entry,@Owner,@Recipe,@Portions;
 END;
 CLOSE entries; DEALLOCATE entries;
END;
GO
EXEC dbo.sp_EnsurePlannedBatches;
GO
CREATE OR ALTER TRIGGER dbo.tr_MealPlan_Batches ON dbo.MealPlanEntries AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF TRIGGER_NESTLEVEL(OBJECT_ID(N'dbo.tr_MealPlan_Batches'))>1 RETURN;
 DECLARE @LegacyServings BIT=CASE WHEN UPDATE(Servings) AND NOT UPDATE(PortionQuantity) THEN 1 ELSE 0 END;
 UPDATE p SET PortionQuantity=CASE WHEN @LegacyServings=1 THEN COALESCE(i.Servings,r.Servings,1) ELSE COALESCE(i.PortionQuantity,i.Servings,r.Servings,1) END,
 Version=COALESCE(d.Version,0)+1
 FROM dbo.MealPlanEntries p JOIN inserted i ON i.MealPlanEntryID=p.MealPlanEntryID LEFT JOIN deleted d ON d.MealPlanEntryID=p.MealPlanEntryID JOIN dbo.Recipes r ON r.RecipeID=p.RecipeID;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.CookingBatches b ON b.CookingBatchID=i.CookingBatchID WHERE b.UserID<>i.UserID OR b.RecipeID<>i.RecipeID) THROW 50002,N'Invalid batch allocation owner or recipe',1;
 DECLARE @User INT;
 DECLARE owners CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT UserID FROM inserted WHERE CookingBatchID IS NULL;
 OPEN owners; FETCH NEXT FROM owners INTO @User;
 WHILE @@FETCH_STATUS=0 BEGIN EXEC dbo.sp_EnsurePlannedBatches @User; FETCH NEXT FROM owners INTO @User; END;
 CLOSE owners; DEALLOCATE owners;
 DECLARE @Affected TABLE(ID INT PRIMARY KEY);
 INSERT @Affected SELECT CookingBatchID FROM dbo.MealPlanEntries WHERE MealPlanEntryID IN(SELECT MealPlanEntryID FROM inserted)
 UNION SELECT CookingBatchID FROM deleted WHERE CookingBatchID IS NOT NULL;
 DECLARE @Locked INT; SELECT @Locked=COUNT(*) FROM dbo.CookingBatches b WITH(UPDLOCK,HOLDLOCK) JOIN @Affected a ON a.ID=b.CookingBatchID;
 UPDATE b SET PlannedPortions=p.Qty FROM dbo.CookingBatches b JOIN @Affected a ON a.ID=b.CookingBatchID
 CROSS APPLY(SELECT COUNT(*) AS N,MAX(PortionQuantity) AS Qty FROM dbo.MealPlanEntries WHERE CookingBatchID=b.CookingBatchID) p
 WHERE b.ResizeWithMeal=1 AND b.CookedAt IS NULL AND p.N=1;
 IF EXISTS(SELECT 1 FROM dbo.MealPlanEntries p JOIN @Affected a ON a.ID=p.CookingBatchID WHERE p.PortionQuantity<=0 OR p.ConsumedPortions<0 OR p.ConsumedPortions>p.PortionQuantity) THROW 50000,N'Invalid allocated portions',1;
 IF EXISTS(SELECT 1 FROM dbo.CookingBatches b JOIN @Affected a ON a.ID=b.CookingBatchID
 CROSS APPLY(SELECT COALESCE(SUM(PortionQuantity-ConsumedPortions),0) AS Reserved FROM dbo.MealPlanEntries WHERE CookingBatchID=b.CookingBatchID) p
 WHERE p.Reserved>COALESCE(b.ProducedPortions,b.PlannedPortions)-b.UsedPortions) THROW 50004,N'Allocated portions exceed available batch portions',1;
 UPDATE b SET Version=Version+1 FROM dbo.CookingBatches b JOIN @Affected a ON a.ID=b.CookingBatchID;
END;
GO
