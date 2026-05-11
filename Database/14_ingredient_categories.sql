USE MealPrepDB;
GO

-- IngredientCategories: lookup table for grouping the Ingrediente list
-- in the app's sidebar (Produse / Lactate si oua / Carne si peste / ...).
-- Augments the original "ingredients are global" decision — the global pool
-- stays, but each row now optionally belongs to a category for display.

IF OBJECT_ID(N'dbo.IngredientCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IngredientCategories (
        IngredientCategoryID INT IDENTITY(1,1) NOT NULL,
        Name                 NVARCHAR(50)      NOT NULL,
        CONSTRAINT PK_IngredientCategories      PRIMARY KEY CLUSTERED (IngredientCategoryID),
        CONSTRAINT UQ_IngredientCategories_Name UNIQUE (Name)
    );
END
GO

-- Optional FK column on Ingredients. Nullable so existing rows survive the
-- migration; the seed file backfills sensible values for the ones we ship with.
IF COL_LENGTH(N'dbo.Ingredients', N'IngredientCategoryID') IS NULL
    ALTER TABLE dbo.Ingredients
        ADD IngredientCategoryID INT NULL
            CONSTRAINT FK_Ingredients_IngredientCategories
            REFERENCES dbo.IngredientCategories(IngredientCategoryID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Ingredients_IngredientCategoryID'
                 AND object_id = OBJECT_ID(N'dbo.Ingredients'))
    CREATE INDEX IX_Ingredients_IngredientCategoryID
        ON dbo.Ingredients(IngredientCategoryID);
GO
