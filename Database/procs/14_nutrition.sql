USE MealPrepDB;
GO

-- ===== sp_GetIngredientNutrition =====
-- Returns the optional nutrition row for one ingredient. Empty result = not filled yet.

CREATE OR ALTER PROCEDURE dbo.sp_GetIngredientNutrition
    @IngredientID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        n.IngredientID,
        i.Name AS IngredientName,
        n.BasisQuantity,
        n.BasisUnitID,
        u.Name AS BasisUnitName,
        u.Abbreviation AS BasisUnitAbbreviation,
        n.Calories,
        n.ProteinGrams,
        n.CarbsGrams,
        n.FatGrams,
        n.UpdatedAt
    FROM dbo.IngredientNutrition n
    JOIN dbo.Ingredients i ON i.IngredientID = n.IngredientID
    JOIN dbo.Units u       ON u.UnitID       = n.BasisUnitID
    WHERE n.IngredientID = @IngredientID;
END
GO

-- ===== sp_SetIngredientNutrition =====
-- Upserts nutrition values for one ingredient, normalized to @BasisQuantity @BasisUnitID.

CREATE OR ALTER PROCEDURE dbo.sp_SetIngredientNutrition
    @IngredientID   INT,
    @BasisQuantity  DECIMAL(10, 2),
    @BasisUnitID    INT,
    @Calories       DECIMAL(10, 2),
    @ProteinGrams   DECIMAL(10, 2),
    @CarbsGrams     DECIMAL(10, 2),
    @FatGrams       DECIMAL(10, 2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        MERGE dbo.IngredientNutrition AS target
        USING (SELECT
            @IngredientID AS IngredientID,
            @BasisQuantity AS BasisQuantity,
            @BasisUnitID AS BasisUnitID,
            @Calories AS Calories,
            @ProteinGrams AS ProteinGrams,
            @CarbsGrams AS CarbsGrams,
            @FatGrams AS FatGrams
        ) AS source
        ON target.IngredientID = source.IngredientID
        WHEN MATCHED THEN
            UPDATE SET
                BasisQuantity = source.BasisQuantity,
                BasisUnitID = source.BasisUnitID,
                Calories = source.Calories,
                ProteinGrams = source.ProteinGrams,
                CarbsGrams = source.CarbsGrams,
                FatGrams = source.FatGrams,
                UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED BY TARGET THEN
            INSERT (IngredientID, BasisQuantity, BasisUnitID, Calories, ProteinGrams, CarbsGrams, FatGrams)
            VALUES (source.IngredientID, source.BasisQuantity, source.BasisUnitID,
                    source.Calories, source.ProteinGrams, source.CarbsGrams, source.FatGrams);

        EXEC dbo.sp_WriteAudit
            @Action     = N'INGREDIENT_NUTRITION_SET',
            @EntityType = N'Ingredient',
            @EntityID   = @IngredientID,
            @Details    = N'Nutrition values updated';

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_DeleteIngredientNutrition =====
-- Clears optional nutrition values for one ingredient.

CREATE OR ALTER PROCEDURE dbo.sp_DeleteIngredientNutrition
    @IngredientID INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        DELETE FROM dbo.IngredientNutrition
        WHERE IngredientID = @IngredientID;

        EXEC dbo.sp_WriteAudit
            @Action     = N'INGREDIENT_NUTRITION_DELETE',
            @EntityType = N'Ingredient',
            @EntityID   = @IngredientID,
            @Details    = N'Nutrition values cleared';

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_GetRecipeNutrition =====
-- Calculates nutrition from ingredient nutrition rows. Ingredients without nutrition,
-- or with units that cannot convert to the nutrition basis unit, are counted separately
-- and excluded from the totals rather than faking precision.

CREATE OR ALTER PROCEDURE dbo.sp_GetRecipeNutrition
    @RecipeID INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH lines AS (
        SELECT
            r.RecipeID,
            r.Servings,
            ri.RecipeIngredientID,
            n.IngredientID AS NutritionIngredientID,
            CASE
                WHEN n.IngredientID IS NULL THEN NULL
                WHEN ri.UnitID = n.BasisUnitID THEN CAST(1.0 AS DECIMAL(18, 8))
                ELSE uc.Multiplier
            END AS MultiplierToBasis,
            ri.Quantity,
            n.BasisQuantity,
            n.Calories,
            n.ProteinGrams,
            n.CarbsGrams,
            n.FatGrams
        FROM dbo.Recipes r
        LEFT JOIN dbo.RecipeIngredients ri ON ri.RecipeID = r.RecipeID
        LEFT JOIN dbo.IngredientNutrition n ON n.IngredientID = ri.IngredientID
        LEFT JOIN dbo.UnitConversions uc ON uc.FromUnitID = ri.UnitID AND uc.ToUnitID = n.BasisUnitID
        WHERE r.RecipeID = @RecipeID
    ), calculated AS (
        SELECT
            RecipeID,
            Servings,
            CASE WHEN NutritionIngredientID IS NOT NULL AND MultiplierToBasis IS NOT NULL
                 THEN Quantity * MultiplierToBasis / BasisQuantity ELSE 0 END AS Factor,
            Calories,
            ProteinGrams,
            CarbsGrams,
            FatGrams,
            CASE WHEN RecipeIngredientID IS NOT NULL AND NutritionIngredientID IS NULL THEN 1 ELSE 0 END AS MissingNutrition,
            CASE WHEN NutritionIngredientID IS NOT NULL AND MultiplierToBasis IS NULL THEN 1 ELSE 0 END AS UnconvertibleIngredient
        FROM lines
    )
    SELECT
        RecipeID,
        Servings,
        CAST(SUM(Factor * ISNULL(Calories, 0)) AS DECIMAL(10, 2)) AS TotalCalories,
        CAST(SUM(Factor * ISNULL(ProteinGrams, 0)) AS DECIMAL(10, 2)) AS TotalProteinGrams,
        CAST(SUM(Factor * ISNULL(CarbsGrams, 0)) AS DECIMAL(10, 2)) AS TotalCarbsGrams,
        CAST(SUM(Factor * ISNULL(FatGrams, 0)) AS DECIMAL(10, 2)) AS TotalFatGrams,
        CAST(SUM(Factor * ISNULL(Calories, 0)) / NULLIF(CAST(Servings AS DECIMAL(10, 2)), 0) AS DECIMAL(10, 2)) AS CaloriesPerServing,
        CAST(SUM(Factor * ISNULL(ProteinGrams, 0)) / NULLIF(CAST(Servings AS DECIMAL(10, 2)), 0) AS DECIMAL(10, 2)) AS ProteinGramsPerServing,
        CAST(SUM(Factor * ISNULL(CarbsGrams, 0)) / NULLIF(CAST(Servings AS DECIMAL(10, 2)), 0) AS DECIMAL(10, 2)) AS CarbsGramsPerServing,
        CAST(SUM(Factor * ISNULL(FatGrams, 0)) / NULLIF(CAST(Servings AS DECIMAL(10, 2)), 0) AS DECIMAL(10, 2)) AS FatGramsPerServing,
        SUM(MissingNutrition) AS MissingNutritionCount,
        SUM(UnconvertibleIngredient) AS UnconvertibleIngredientCount
    FROM calculated
    GROUP BY RecipeID, Servings;
END
GO
