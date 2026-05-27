USE MealPrepDB;
GO

IF OBJECT_ID(N'dbo.IngredientNutrition', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IngredientNutrition (
        IngredientID   INT            NOT NULL,
        BasisQuantity  DECIMAL(10, 2) NOT NULL CONSTRAINT DF_IngredientNutrition_BasisQuantity DEFAULT (100),
        BasisUnitID    INT            NOT NULL,
        Calories       DECIMAL(10, 2) NOT NULL,
        ProteinGrams   DECIMAL(10, 2) NOT NULL CONSTRAINT DF_IngredientNutrition_ProteinGrams DEFAULT (0),
        CarbsGrams     DECIMAL(10, 2) NOT NULL CONSTRAINT DF_IngredientNutrition_CarbsGrams DEFAULT (0),
        FatGrams       DECIMAL(10, 2) NOT NULL CONSTRAINT DF_IngredientNutrition_FatGrams DEFAULT (0),
        UpdatedAt      DATETIME2(0)   NOT NULL CONSTRAINT DF_IngredientNutrition_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_IngredientNutrition PRIMARY KEY CLUSTERED (IngredientID),
        CONSTRAINT FK_IngredientNutrition_Ingredient FOREIGN KEY (IngredientID) REFERENCES dbo.Ingredients(IngredientID),
        CONSTRAINT FK_IngredientNutrition_BasisUnit  FOREIGN KEY (BasisUnitID)  REFERENCES dbo.Units(UnitID),
        CONSTRAINT CK_IngredientNutrition_BasisQuantity CHECK (BasisQuantity > 0),
        CONSTRAINT CK_IngredientNutrition_Calories      CHECK (Calories >= 0),
        CONSTRAINT CK_IngredientNutrition_ProteinGrams  CHECK (ProteinGrams >= 0),
        CONSTRAINT CK_IngredientNutrition_CarbsGrams    CHECK (CarbsGrams >= 0),
        CONSTRAINT CK_IngredientNutrition_FatGrams      CHECK (FatGrams >= 0)
    );
END
GO
