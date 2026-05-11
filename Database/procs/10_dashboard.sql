USE MealPrepDB;
GO

-- ===== sp_GetDashboardCounts =====
-- One row, 4 columns matching the Acasa tile row:
--   RecipesActiveCount         -- this user's recipes
--   IngredientsCount           -- global ingredients table size
--   MealsPlannedFromTodayCount -- this user's planned meals from today onward
--   FavoritesCount             -- this user's favorited recipes
--
-- The "from today" cutoff means the tile reflects "upcoming meals", which is
-- what the user actually cares about on the home screen.

CREATE OR ALTER PROCEDURE dbo.sp_GetDashboardCounts
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(SYSUTCDATETIME() AS DATE);

    SELECT
        (SELECT COUNT(*) FROM dbo.Recipes WHERE UserID = @UserID)               AS RecipesActiveCount,
        (SELECT COUNT(*) FROM dbo.Ingredients)                                  AS IngredientsCount,
        (SELECT COUNT(*) FROM dbo.MealPlanEntries
            WHERE UserID = @UserID AND PlannedDate >= @Today)                   AS MealsPlannedFromTodayCount,
        (SELECT COUNT(*) FROM dbo.RecipeFavorites WHERE UserID = @UserID)       AS FavoritesCount;
END
GO

-- ===== sp_GetRecentRecipes =====
-- Recently created or updated recipes belonging to @UserID. Used by the Acasa
-- "Retete Recente" grid. We sort by ISNULL(UpdatedAt, CreatedAt) DESC so a
-- never-updated recipe still surfaces by its CreatedAt timestamp.

CREATE OR ALTER PROCEDURE dbo.sp_GetRecentRecipes
    @UserID INT,
    @TopN   INT = 12
AS
BEGIN
    SET NOCOUNT ON;

    IF @TopN IS NULL OR @TopN < 1 SET @TopN = 12;
    IF @TopN > 200 SET @TopN = 200;

    SELECT TOP (@TopN)
        r.RecipeID,
        r.Title,
        r.CategoryID,
        c.Name             AS CategoryName,
        r.PrepTimeMinutes,
        r.CookTimeMinutes,
        r.Servings,
        r.CreatedAt,
        r.UpdatedAt,
        ISNULL(r.UpdatedAt, r.CreatedAt) AS LastTouchedAt
    FROM dbo.Recipes r
    LEFT JOIN dbo.Categories c ON c.CategoryID = r.CategoryID
    WHERE r.UserID = @UserID
    ORDER BY ISNULL(r.UpdatedAt, r.CreatedAt) DESC, r.RecipeID DESC;
END
GO
