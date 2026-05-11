USE MealPrepDB;
GO

-- ===== sp_GetShoppingList =====
-- Computed view, not stored. Aggregates ingredient demand from planned meals
-- in [@StartDate, @EndDate] minus what's already in the user's pantry, and
-- returns one row per (Ingredient, Unit) that the user still needs to buy.
--
-- Servings scaling:
--   demand = recipe_ingredient.Quantity
--          * ISNULL(planned.Servings, 1)
--          / NULLIF(recipe.Servings, 0)
-- so planning "6 servings of a 4-serving recipe" demands 1.5x of each
-- ingredient. If the recipe has no Servings set, we treat it as 1 (no scaling).
--
-- Unit matching is exact: "500 g flour" and "2 cups flour" are NOT merged.
-- That's a v1 limitation; cross-unit conversion is future work.

CREATE OR ALTER PROCEDURE dbo.sp_GetShoppingList
    @UserID    INT,
    @StartDate DATE,
    @EndDate   DATE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH demanded AS (
        SELECT
            ri.IngredientID,
            ri.UnitID,
            SUM(
                ri.Quantity
                * ISNULL(mpe.Servings, 1)
                / NULLIF(r.Servings, 0)
            ) AS NeededQty
        FROM dbo.MealPlanEntries mpe
        JOIN dbo.Recipes r            ON r.RecipeID = mpe.RecipeID
        JOIN dbo.RecipeIngredients ri ON ri.RecipeID = mpe.RecipeID
        WHERE mpe.UserID = @UserID
          AND mpe.PlannedDate BETWEEN @StartDate AND @EndDate
        GROUP BY ri.IngredientID, ri.UnitID
    )
    SELECT
        d.IngredientID,
        i.Name           AS IngredientName,
        d.UnitID,
        u.Abbreviation   AS UnitAbbreviation,
        u.Name           AS UnitName,
        d.NeededQty,
        ISNULL(p.Quantity, 0)                AS OnHandQty,
        d.NeededQty - ISNULL(p.Quantity, 0)  AS ToBuyQty
    FROM demanded d
    JOIN dbo.Ingredients i ON i.IngredientID = d.IngredientID
    JOIN dbo.Units u       ON u.UnitID       = d.UnitID
    LEFT JOIN dbo.UserPantry p
        ON p.UserID       = @UserID
       AND p.IngredientID = d.IngredientID
       AND p.UnitID       = d.UnitID
    WHERE d.NeededQty IS NOT NULL
      AND d.NeededQty - ISNULL(p.Quantity, 0) > 0
    ORDER BY i.Name, u.Abbreviation;
END
GO
