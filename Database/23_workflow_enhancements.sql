USE MealPrepDB;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.RecipeIngredients') AND name=N'Quantity' AND scale<>4) ALTER TABLE dbo.RecipeIngredients ALTER COLUMN Quantity DECIMAL(18,4) NOT NULL;
GO
IF COL_LENGTH(N'dbo.Ingredients',N'RowVersion') IS NULL ALTER TABLE dbo.Ingredients ADD RowVersion ROWVERSION;
GO
IF OBJECT_ID(N'dbo.PlannerMoves',N'U') IS NULL
 CREATE TABLE dbo.PlannerMoves(MoveID INT IDENTITY PRIMARY KEY,UserID INT NOT NULL REFERENCES dbo.Users(UserID),MealPlanEntryID INT NOT NULL,
 CookingBatchID INT NULL,OldState NVARCHAR(MAX) NOT NULL CHECK(ISJSON(OldState)=1),AfterVersion BIGINT NOT NULL,BatchVersion BIGINT NULL,
 CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),UndoneAt DATETIME2(7) NULL);
GO
-- A photo mutation participates in the recipe's concurrency token, including legacy clients.
CREATE OR ALTER TRIGGER dbo.tr_RecipePhotos_RecipeVersion ON dbo.RecipePhotos AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 UPDATE dbo.Recipes SET UpdatedAt=SYSUTCDATETIME() WHERE RecipeID IN(SELECT RecipeID FROM inserted UNION SELECT RecipeID FROM deleted);
END;
GO
