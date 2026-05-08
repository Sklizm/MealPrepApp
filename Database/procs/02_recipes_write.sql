USE MealPrepDB;
GO

-- ===== sp_CreateRecipe =====
-- Creates a recipe and its ingredient lines in one transaction.
-- @IngredientsJson format:
--   [
--     { "IngredientID": 1, "UnitID": 3, "Quantity": 100.0, "Notes": "diced" },
--     ...
--   ]
-- Returns: new RecipeID via SELECT.

CREATE OR ALTER PROCEDURE dbo.sp_CreateRecipe
    @UserID           INT,
    @CategoryID       INT            = NULL,
    @Title            NVARCHAR(150),
    @Description      NVARCHAR(MAX)  = NULL,
    @Instructions     NVARCHAR(MAX),
    @PrepTimeMinutes  INT            = NULL,
    @CookTimeMinutes  INT            = NULL,
    @Servings         INT            = NULL,
    @IngredientsJson  NVARCHAR(MAX)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        INSERT INTO dbo.Recipes
            (UserID, CategoryID, Title, Description, Instructions,
             PrepTimeMinutes, CookTimeMinutes, Servings)
        VALUES
            (@UserID, @CategoryID, @Title, @Description, @Instructions,
             @PrepTimeMinutes, @CookTimeMinutes, @Servings);

        DECLARE @RecipeID INT = SCOPE_IDENTITY();

        IF @IngredientsJson IS NOT NULL AND LEN(@IngredientsJson) > 0
        BEGIN
            INSERT INTO dbo.RecipeIngredients (RecipeID, IngredientID, UnitID, Quantity, Notes)
            SELECT
                @RecipeID,
                j.IngredientID,
                j.UnitID,
                j.Quantity,
                j.Notes
            FROM OPENJSON(@IngredientsJson)
                WITH (
                    IngredientID INT             '$.IngredientID',
                    UnitID       INT             '$.UnitID',
                    Quantity     DECIMAL(10,2)   '$.Quantity',
                    Notes        NVARCHAR(255)   '$.Notes'
                ) AS j;
        END

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'RECIPE_CREATE',
            @EntityType = N'Recipe',
            @EntityID   = @RecipeID,
            @Details    = @Title;

        COMMIT TRAN;

        SELECT @RecipeID AS RecipeID;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_UpdateRecipe =====
-- Updates a recipe owned by @UserID. THROWs 50002 if not the owner.
-- If @IngredientsJson is NULL, ingredients are left untouched.
-- If @IngredientsJson is supplied (even '[]'), the ingredient list is replaced.

CREATE OR ALTER PROCEDURE dbo.sp_UpdateRecipe
    @RecipeID         INT,
    @UserID           INT,
    @CategoryID       INT            = NULL,
    @Title            NVARCHAR(150),
    @Description      NVARCHAR(MAX)  = NULL,
    @Instructions     NVARCHAR(MAX),
    @PrepTimeMinutes  INT            = NULL,
    @CookTimeMinutes  INT            = NULL,
    @Servings         INT            = NULL,
    @IngredientsJson  NVARCHAR(MAX)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @OwnerID INT = (SELECT UserID FROM dbo.Recipes WHERE RecipeID = @RecipeID);

        IF @OwnerID IS NULL
            THROW 50003, N'Recipe not found', 1;

        IF @OwnerID <> @UserID
            THROW 50002, N'Not authorized to modify this recipe', 1;

        UPDATE dbo.Recipes
        SET CategoryID      = @CategoryID,
            Title           = @Title,
            Description     = @Description,
            Instructions    = @Instructions,
            PrepTimeMinutes = @PrepTimeMinutes,
            CookTimeMinutes = @CookTimeMinutes,
            Servings        = @Servings,
            UpdatedAt       = SYSUTCDATETIME()
        WHERE RecipeID = @RecipeID;

        IF @IngredientsJson IS NOT NULL
        BEGIN
            DELETE FROM dbo.RecipeIngredients WHERE RecipeID = @RecipeID;

            IF LEN(@IngredientsJson) > 0
            BEGIN
                INSERT INTO dbo.RecipeIngredients (RecipeID, IngredientID, UnitID, Quantity, Notes)
                SELECT @RecipeID, j.IngredientID, j.UnitID, j.Quantity, j.Notes
                FROM OPENJSON(@IngredientsJson)
                    WITH (
                        IngredientID INT             '$.IngredientID',
                        UnitID       INT             '$.UnitID',
                        Quantity     DECIMAL(10,2)   '$.Quantity',
                        Notes        NVARCHAR(255)   '$.Notes'
                    ) AS j;
            END
        END

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'RECIPE_UPDATE',
            @EntityType = N'Recipe',
            @EntityID   = @RecipeID,
            @Details    = @Title;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_DeleteRecipe =====
-- Deletes a recipe (RecipeIngredients cascade). Owner-only.

CREATE OR ALTER PROCEDURE dbo.sp_DeleteRecipe
    @RecipeID INT,
    @UserID   INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @OwnerID INT = (SELECT UserID FROM dbo.Recipes WHERE RecipeID = @RecipeID);

        IF @OwnerID IS NULL
            THROW 50003, N'Recipe not found', 1;

        IF @OwnerID <> @UserID
            THROW 50002, N'Not authorized to delete this recipe', 1;

        DELETE FROM dbo.Recipes WHERE RecipeID = @RecipeID;

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'RECIPE_DELETE',
            @EntityType = N'Recipe',
            @EntityID   = @RecipeID;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO
