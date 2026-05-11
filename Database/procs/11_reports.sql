USE MealPrepDB;
GO

-- ===== sp_GetMonthlyStats =====
-- One row of headline numbers for the "Statistici lunare" sub-tab of Rapoarte.
-- All counts are scoped to @UserID and the given calendar month on PlannedDate.

CREATE OR ALTER PROCEDURE dbo.sp_GetMonthlyStats
    @UserID INT,
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH scoped AS (
        SELECT mpe.MealPlanEntryID, mpe.RecipeID, mpe.CategoryID, c.Name AS CategoryName
        FROM dbo.MealPlanEntries mpe
        JOIN dbo.Categories c ON c.CategoryID = mpe.CategoryID
        WHERE mpe.UserID = @UserID
          AND YEAR(mpe.PlannedDate)  = @Year
          AND MONTH(mpe.PlannedDate) = @Month
    )
    SELECT
        (SELECT COUNT(*) FROM scoped)                                                AS TotalMealsPlanned,
        (SELECT COUNT(*) FROM scoped WHERE CategoryName = N'Breakfast')              AS BreakfastCount,
        (SELECT COUNT(*) FROM scoped WHERE CategoryName = N'Lunch')                  AS LunchCount,
        (SELECT COUNT(*) FROM scoped WHERE CategoryName = N'Dinner')                 AS DinnerCount,
        (SELECT COUNT(*) FROM scoped WHERE CategoryName = N'Snack')                  AS SnackCount,
        (SELECT COUNT(*) FROM scoped WHERE CategoryName = N'Dessert')                AS DessertCount,
        (SELECT COUNT(*) FROM scoped WHERE CategoryName = N'Drink')                  AS DrinkCount,
        (SELECT COUNT(DISTINCT RecipeID) FROM scoped)                                AS DistinctRecipes,
        (SELECT COUNT(DISTINCT ri.IngredientID)
            FROM scoped s JOIN dbo.RecipeIngredients ri ON ri.RecipeID = s.RecipeID) AS DistinctIngredients;
END
GO

-- ===== sp_GetTopRecipes =====
-- Recipes a user planned most often in a given month.

CREATE OR ALTER PROCEDURE dbo.sp_GetTopRecipes
    @UserID INT,
    @Year   INT,
    @Month  INT,
    @TopN   INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    IF @TopN IS NULL OR @TopN < 1 SET @TopN = 5;
    IF @TopN > 100 SET @TopN = 100;

    SELECT TOP (@TopN)
        r.RecipeID,
        r.Title,
        c.Name AS CategoryName,
        COUNT(*) AS PlannedCount
    FROM dbo.MealPlanEntries mpe
    JOIN dbo.Recipes r         ON r.RecipeID    = mpe.RecipeID
    LEFT JOIN dbo.Categories c ON c.CategoryID  = r.CategoryID
    WHERE mpe.UserID = @UserID
      AND YEAR(mpe.PlannedDate)  = @Year
      AND MONTH(mpe.PlannedDate) = @Month
    GROUP BY r.RecipeID, r.Title, c.Name
    ORDER BY COUNT(*) DESC, r.Title;
END
GO

-- ===== sp_GetTopIngredients =====
-- Ingredients most frequently appearing in a user's planned meals for a given month.
-- Count = number of (meal plan entry × recipe ingredient line) rows. Quantities
-- are deliberately ignored — this is "how often does this ingredient show up",
-- not "how much of it do you eat".

CREATE OR ALTER PROCEDURE dbo.sp_GetTopIngredients
    @UserID INT,
    @Year   INT,
    @Month  INT,
    @TopN   INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    IF @TopN IS NULL OR @TopN < 1 SET @TopN = 10;
    IF @TopN > 100 SET @TopN = 100;

    SELECT TOP (@TopN)
        i.IngredientID,
        i.Name,
        COUNT(*) AS UsageCount
    FROM dbo.MealPlanEntries mpe
    JOIN dbo.RecipeIngredients ri ON ri.RecipeID = mpe.RecipeID
    JOIN dbo.Ingredients i        ON i.IngredientID = ri.IngredientID
    WHERE mpe.UserID = @UserID
      AND YEAR(mpe.PlannedDate)  = @Year
      AND MONTH(mpe.PlannedDate) = @Month
    GROUP BY i.IngredientID, i.Name
    ORDER BY COUNT(*) DESC, i.Name;
END
GO
