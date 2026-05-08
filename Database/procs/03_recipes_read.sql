USE MealPrepDB;
GO

-- ===== sp_GetRecipeFull =====
-- Returns two result sets:
--   (1) recipe header + author + category names
--   (2) ingredient lines with names + unit abbreviations
-- App reads both with NextResult().

CREATE OR ALTER PROCEDURE dbo.sp_GetRecipeFull
    @RecipeID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.RecipeID,
        r.Title,
        r.Description,
        r.Instructions,
        r.PrepTimeMinutes,
        r.CookTimeMinutes,
        r.Servings,
        r.CreatedAt,
        r.UpdatedAt,
        r.UserID,
        u.Username   AS AuthorUsername,
        r.CategoryID,
        c.Name       AS CategoryName
    FROM dbo.Recipes r
    JOIN dbo.Users u      ON u.UserID     = r.UserID
    LEFT JOIN dbo.Categories c ON c.CategoryID = r.CategoryID
    WHERE r.RecipeID = @RecipeID;

    SELECT
        ri.RecipeIngredientID,
        ri.IngredientID,
        ing.Name        AS IngredientName,
        ri.UnitID,
        un.Name         AS UnitName,
        un.Abbreviation AS UnitAbbreviation,
        ri.Quantity,
        ri.Notes
    FROM dbo.RecipeIngredients ri
    JOIN dbo.Ingredients ing ON ing.IngredientID = ri.IngredientID
    JOIN dbo.Units un        ON un.UnitID       = ri.UnitID
    WHERE ri.RecipeID = @RecipeID
    ORDER BY ri.RecipeIngredientID;
END
GO

-- ===== sp_GetRecipes =====
-- Paged list. NULL filters mean "no filter".
-- Returns columns include TotalCount via window function so the app
-- knows how many pages exist without a second query.

CREATE OR ALTER PROCEDURE dbo.sp_GetRecipes
    @UserID     INT = NULL,
    @CategoryID INT = NULL,
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
        COUNT(*) OVER () AS TotalCount
    FROM dbo.Recipes r
    JOIN dbo.Users u      ON u.UserID     = r.UserID
    LEFT JOIN dbo.Categories c ON c.CategoryID = r.CategoryID
    WHERE (@UserID     IS NULL OR r.UserID     = @UserID)
      AND (@CategoryID IS NULL OR r.CategoryID = @CategoryID)
    ORDER BY r.CreatedAt DESC, r.RecipeID DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ===== sp_SearchRecipesByTitle =====
-- Substring match on Title. Parameterized — safe against SQL injection.

CREATE OR ALTER PROCEDURE dbo.sp_SearchRecipesByTitle
    @Term       NVARCHAR(150),
    @PageNumber INT = 1,
    @PageSize   INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 OR @PageSize > 200 SET @PageSize = 20;

    DECLARE @Pattern NVARCHAR(160) = N'%' + @Term + N'%';

    SELECT
        r.RecipeID,
        r.Title,
        r.UserID,
        u.Username      AS AuthorUsername,
        r.CategoryID,
        c.Name          AS CategoryName,
        r.CreatedAt,
        COUNT(*) OVER () AS TotalCount
    FROM dbo.Recipes r
    JOIN dbo.Users u      ON u.UserID     = r.UserID
    LEFT JOIN dbo.Categories c ON c.CategoryID = r.CategoryID
    WHERE r.Title LIKE @Pattern
    ORDER BY r.CreatedAt DESC, r.RecipeID DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ===== sp_FindRecipesByIngredients =====
-- "What can I make with ___?" Returns recipes where the count of matching
-- ingredients >= @MinMatchCount. Default behavior: must match ALL listed.
--
-- Returns: RecipeID, Title, MatchedIngredients, TotalIngredients, AuthorUsername,
--          CategoryName. Sorted by best match (matched DESC, total ASC).

CREATE OR ALTER PROCEDURE dbo.sp_FindRecipesByIngredients
    @IngredientIDs   dbo.IntList READONLY,
    @MinMatchCount   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Listed INT = (SELECT COUNT(*) FROM @IngredientIDs);
    IF @MinMatchCount IS NULL
        SET @MinMatchCount = @Listed;

    -- Empty list: return nothing rather than every recipe.
    IF @Listed = 0
        RETURN;

    SELECT
        r.RecipeID,
        r.Title,
        r.UserID,
        u.Username      AS AuthorUsername,
        r.CategoryID,
        c.Name          AS CategoryName,
        matched.MatchedCount    AS MatchedIngredients,
        total.TotalCount        AS TotalIngredients
    FROM dbo.Recipes r
    JOIN dbo.Users u      ON u.UserID     = r.UserID
    LEFT JOIN dbo.Categories c ON c.CategoryID = r.CategoryID
    CROSS APPLY (
        SELECT COUNT(*) AS MatchedCount
        FROM dbo.RecipeIngredients ri
        WHERE ri.RecipeID = r.RecipeID
          AND ri.IngredientID IN (SELECT ID FROM @IngredientIDs)
    ) matched
    CROSS APPLY (
        SELECT COUNT(*) AS TotalCount
        FROM dbo.RecipeIngredients ri
        WHERE ri.RecipeID = r.RecipeID
    ) total
    WHERE matched.MatchedCount >= @MinMatchCount
    ORDER BY matched.MatchedCount DESC, total.TotalCount ASC, r.RecipeID;
END
GO
