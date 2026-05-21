USE MealPrepDB;
GO

-- ===== sp_AddIngredient =====
-- Adds a global ingredient. Errors on duplicate Name (UQ constraint).
-- @IngredientCategoryID is optional; NULL leaves the ingredient ungrouped.
-- Returns: new IngredientID.

CREATE OR ALTER PROCEDURE dbo.sp_AddIngredient
    @Name                  NVARCHAR(100),
    @DefaultUnitID         INT = NULL,
    @IngredientCategoryID  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        INSERT INTO dbo.Ingredients (Name, DefaultUnitID, IngredientCategoryID)
        VALUES (@Name, @DefaultUnitID, @IngredientCategoryID);

        DECLARE @NewID INT = SCOPE_IDENTITY();

        EXEC dbo.sp_WriteAudit
            @Action     = N'INGREDIENT_ADD',
            @EntityType = N'Ingredient',
            @EntityID   = @NewID,
            @Details    = @Name;

        COMMIT TRAN;
        SELECT @NewID AS IngredientID;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_GetIngredients =====
-- Full ingredient list with default unit + category info, sorted by name.
-- Optional @IngredientCategoryID filter — NULL means "no filter, return all".
-- Used to populate dropdowns/autocomplete and the Ingrediente list view.

CREATE OR ALTER PROCEDURE dbo.sp_GetIngredients
    @IngredientCategoryID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        i.IngredientID,
        i.Name,
        i.DefaultUnitID,
        u.Name         AS DefaultUnitName,
        u.Abbreviation AS DefaultUnitAbbreviation,
        i.IngredientCategoryID,
        ic.Name        AS IngredientCategoryName
    FROM dbo.Ingredients i
    LEFT JOIN dbo.Units u                ON u.UnitID                = i.DefaultUnitID
    LEFT JOIN dbo.IngredientCategories ic ON ic.IngredientCategoryID = i.IngredientCategoryID
    WHERE @IngredientCategoryID IS NULL
       OR i.IngredientCategoryID = @IngredientCategoryID
    ORDER BY i.Name;
END
GO

-- ===== sp_SearchIngredients =====
-- Substring match on Name. Used by autocomplete in the recipe editor.

CREATE OR ALTER PROCEDURE dbo.sp_SearchIngredients
    @Term NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Pattern NVARCHAR(110) = N'%' + @Term + N'%';
    SELECT TOP 50
        i.IngredientID,
        i.Name,
        i.DefaultUnitID,
        u.Abbreviation AS DefaultUnitAbbreviation
    FROM dbo.Ingredients i
    LEFT JOIN dbo.Units u ON u.UnitID = i.DefaultUnitID
    WHERE i.Name LIKE @Pattern
    ORDER BY i.Name;
END
GO

-- ===== sp_GetIngredientUsage =====
-- Returns how many RecipeIngredient rows reference this ingredient.
-- App calls this before attempting a delete: if RecipeCount > 0, the
-- delete would be blocked by FK RESTRICT, so show a useful message instead.
-- Returns no rows for a non-existent IngredientID (caller can treat as 0).

CREATE OR ALTER PROCEDURE dbo.sp_GetIngredientUsage
    @IngredientID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        i.IngredientID,
        i.Name,
        COUNT(ri.RecipeIngredientID) AS RecipeCount
    FROM dbo.Ingredients i
    LEFT JOIN dbo.RecipeIngredients ri ON ri.IngredientID = i.IngredientID
    WHERE i.IngredientID = @IngredientID
    GROUP BY i.IngredientID, i.Name;
END
GO
