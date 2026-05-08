USE MealPrepDB;
GO

-- ===== sp_AddIngredient =====
-- Adds a global ingredient. Errors on duplicate Name (UQ constraint).
-- Returns: new IngredientID.

CREATE OR ALTER PROCEDURE dbo.sp_AddIngredient
    @Name           NVARCHAR(100),
    @DefaultUnitID  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        INSERT INTO dbo.Ingredients (Name, DefaultUnitID)
        VALUES (@Name, @DefaultUnitID);

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
-- Full ingredient list with default unit info, sorted by name.
-- Used to populate dropdowns/autocomplete.

CREATE OR ALTER PROCEDURE dbo.sp_GetIngredients
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        i.IngredientID,
        i.Name,
        i.DefaultUnitID,
        u.Name         AS DefaultUnitName,
        u.Abbreviation AS DefaultUnitAbbreviation
    FROM dbo.Ingredients i
    LEFT JOIN dbo.Units u ON u.UnitID = i.DefaultUnitID
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
