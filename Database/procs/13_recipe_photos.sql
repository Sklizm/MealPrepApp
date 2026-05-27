USE MealPrepDB;
GO

-- ===== sp_SetRecipePhoto =====
-- Sets (inserts or replaces) the single photo for a recipe. Owner-only. The app downscales +
-- re-encodes the image to JPEG before calling this, so @ImageData is already small.
--   THROW 50002 — not the owner
--   THROW 50003 — recipe not found

CREATE OR ALTER PROCEDURE dbo.sp_SetRecipePhoto
    @RecipeID    INT,
    @UserID      INT,
    @ImageData   VARBINARY(MAX),
    @ContentType NVARCHAR(100)
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

        IF EXISTS (SELECT 1 FROM dbo.RecipePhotos WHERE RecipeID = @RecipeID)
            UPDATE dbo.RecipePhotos
            SET ImageData   = @ImageData,
                ContentType = @ContentType,
                UpdatedAt   = SYSUTCDATETIME()
            WHERE RecipeID = @RecipeID;
        ELSE
            INSERT INTO dbo.RecipePhotos (RecipeID, ImageData, ContentType)
            VALUES (@RecipeID, @ImageData, @ContentType);

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'RECIPE_PHOTO_SET',
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

-- ===== sp_GetRecipePhoto =====
-- Returns the photo bytes + content type for a recipe, or no rows if none.
-- A read like sp_GetRecipeFull — no ownership check.

CREATE OR ALTER PROCEDURE dbo.sp_GetRecipePhoto
    @RecipeID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ImageData, ContentType
    FROM dbo.RecipePhotos
    WHERE RecipeID = @RecipeID;
END
GO

-- ===== sp_DeleteRecipePhoto =====
-- Removes a recipe's photo. Owner-only. Silent if there is no photo.
--   THROW 50002 — not the owner
--   THROW 50003 — recipe not found

CREATE OR ALTER PROCEDURE dbo.sp_DeleteRecipePhoto
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
            THROW 50002, N'Not authorized to modify this recipe', 1;

        DELETE FROM dbo.RecipePhotos WHERE RecipeID = @RecipeID;

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'RECIPE_PHOTO_DELETE',
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
