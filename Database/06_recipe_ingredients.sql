USE MealPrepDB;
GO

IF OBJECT_ID(N'dbo.RecipeIngredients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeIngredients (
        RecipeIngredientID INT IDENTITY(1,1) NOT NULL,
        RecipeID           INT               NOT NULL,
        IngredientID       INT               NOT NULL,
        UnitID             INT               NOT NULL,
        Quantity           DECIMAL(10, 2)    NOT NULL,
        Notes              NVARCHAR(255)     NULL,
        CONSTRAINT PK_RecipeIngredients               PRIMARY KEY CLUSTERED (RecipeIngredientID),
        CONSTRAINT UQ_RecipeIngredients_Recipe_Ingr  UNIQUE (RecipeID, IngredientID),
        CONSTRAINT FK_RecipeIngredients_Recipes      FOREIGN KEY (RecipeID)     REFERENCES dbo.Recipes(RecipeID) ON DELETE CASCADE,
        CONSTRAINT FK_RecipeIngredients_Ingredients  FOREIGN KEY (IngredientID) REFERENCES dbo.Ingredients(IngredientID),
        CONSTRAINT FK_RecipeIngredients_Units        FOREIGN KEY (UnitID)       REFERENCES dbo.Units(UnitID),
        CONSTRAINT CK_RecipeIngredients_Quantity     CHECK (Quantity > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RecipeIngredients_IngredientID' AND object_id = OBJECT_ID(N'dbo.RecipeIngredients'))
    CREATE INDEX IX_RecipeIngredients_IngredientID ON dbo.RecipeIngredients(IngredientID);
GO
