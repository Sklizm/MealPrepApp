USE MealPrepDB;
GO

-- ===== sp_ToggleFavorite =====
-- If (UserID, RecipeID) row exists -> delete it; else insert.
-- Returns single column IsFavorite: 1 if now favorited, 0 if just unfavorited.
-- App calls this on the heart-icon click and updates the icon state from the result.

CREATE OR ALTER PROCEDURE dbo.sp_ToggleFavorite
    @UserID   INT,
    @RecipeID INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @NewState BIT;

    BEGIN TRY
        BEGIN TRAN;

        IF EXISTS (SELECT 1 FROM dbo.RecipeFavorites WHERE UserID = @UserID AND RecipeID = @RecipeID)
        BEGIN
            DELETE FROM dbo.RecipeFavorites WHERE UserID = @UserID AND RecipeID = @RecipeID;
            SET @NewState = 0;
        END
        ELSE
        BEGIN
            INSERT INTO dbo.RecipeFavorites (UserID, RecipeID) VALUES (@UserID, @RecipeID);
            SET @NewState = 1;
        END

        DECLARE @Details NVARCHAR(500) = CASE WHEN @NewState = 1 THEN N'on' ELSE N'off' END;
        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'FAVORITE_TOGGLE',
            @EntityType = N'Recipe',
            @EntityID   = @RecipeID,
            @Details    = @Details;

        COMMIT TRAN;
        SELECT @NewState AS IsFavorite;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_GetFavoriteRecipes =====
-- Same output shape as sp_GetRecipes so the VM can reuse the same Recipe model.

CREATE OR ALTER PROCEDURE dbo.sp_GetFavoriteRecipes
    @UserID     INT,
    @PageNumber INT = 1,
    @PageSize   INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 OR @PageSize > 200 SET @PageSize = 20;

    SELECT
        r.RecipeID,
        r.Title,
        r.UserID,
        u.Username      AS AuthorUsername,
        r.CategoryID,
        c.Name          AS CategoryName,
        r.PrepTimeMinutes,
        r.CookTimeMinutes,
        r.Servings,
        r.CreatedAt,
        rf.FavoritedAt,
        COUNT(*) OVER () AS TotalCount
    FROM dbo.RecipeFavorites rf
    JOIN dbo.Recipes r         ON r.RecipeID    = rf.RecipeID
    JOIN dbo.Users u           ON u.UserID      = r.UserID
    LEFT JOIN dbo.Categories c ON c.CategoryID  = r.CategoryID
    WHERE rf.UserID = @UserID
    ORDER BY rf.FavoritedAt DESC, r.RecipeID DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO
