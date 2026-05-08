USE MealPrepDB;
GO

IF OBJECT_ID(N'dbo.Ingredients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Ingredients (
        IngredientID    INT IDENTITY(1,1) NOT NULL,
        Name            NVARCHAR(100)     NOT NULL,
        DefaultUnitID   INT               NULL,
        CONSTRAINT PK_Ingredients         PRIMARY KEY CLUSTERED (IngredientID),
        CONSTRAINT UQ_Ingredients_Name    UNIQUE (Name),
        CONSTRAINT FK_Ingredients_Units   FOREIGN KEY (DefaultUnitID) REFERENCES dbo.Units(UnitID)
    );
END
GO
