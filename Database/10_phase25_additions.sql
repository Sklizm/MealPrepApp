USE MealPrepDB;
GO

-- Phase 2.5 additions:
--   1. Missing FK-column indexes (cheap perf hygiene).
--   2. RowVersion column on Recipes for optimistic concurrency in sp_UpdateRecipe.
-- Idempotent.

-- ----- FK index gaps -----

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Ingredients_DefaultUnitID'
      AND object_id = OBJECT_ID(N'dbo.Ingredients')
)
    CREATE INDEX IX_Ingredients_DefaultUnitID
        ON dbo.Ingredients(DefaultUnitID);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RecipeIngredients_UnitID'
      AND object_id = OBJECT_ID(N'dbo.RecipeIngredients')
)
    CREATE INDEX IX_RecipeIngredients_UnitID
        ON dbo.RecipeIngredients(UnitID);
GO

-- ----- RowVersion on Recipes -----
-- ROWVERSION (alias for TIMESTAMP) is auto-maintained by SQL Server on every UPDATE.
-- sp_UpdateRecipe uses it as an optimistic-concurrency token.

IF COL_LENGTH(N'dbo.Recipes', N'RowVersion') IS NULL
    ALTER TABLE dbo.Recipes ADD RowVersion ROWVERSION NOT NULL;
GO
