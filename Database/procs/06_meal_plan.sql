USE MealPrepDB;
GO

-- ===== sp_PlanMeal =====
-- Plans a recipe into a specific day + meal slot (Category).
-- The DB allows any Category as a meal slot; the weekly UI only renders 4 of the 6,
-- but a Dessert/Drink entry is still valid and will show up in monthly view.
-- Returns: new MealPlanEntryID via SELECT.

CREATE OR ALTER PROCEDURE dbo.sp_PlanMeal
    @UserID       INT,
    @RecipeID     INT,
    @CategoryID   INT,
    @PlannedDate  DATE,
    @Servings     INT            = NULL,
    @Notes        NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        INSERT INTO dbo.MealPlanEntries
            (UserID, RecipeID, CategoryID, PlannedDate, Servings, Notes)
        VALUES
            (@UserID, @RecipeID, @CategoryID, @PlannedDate, @Servings, @Notes);

        DECLARE @NewID INT = SCOPE_IDENTITY();

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'MEAL_PLAN_ADD',
            @EntityType = N'MealPlanEntry',
            @EntityID   = @NewID,
            @Details    = NULL;

        COMMIT TRAN;
        SELECT @NewID AS MealPlanEntryID;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_UpdatePlannedMeal =====
-- Moves / re-slots / re-portions an existing planned meal.
--   THROW 50003 — entry not found
--   THROW 50002 — not your entry

CREATE OR ALTER PROCEDURE dbo.sp_UpdatePlannedMeal
    @MealPlanEntryID INT,
    @UserID          INT,
    @CategoryID      INT,
    @PlannedDate     DATE,
    @Servings        INT            = NULL,
    @Notes           NVARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @OwnerID INT = (SELECT UserID FROM dbo.MealPlanEntries WHERE MealPlanEntryID = @MealPlanEntryID);

        IF @OwnerID IS NULL
            THROW 50003, N'Meal plan entry not found', 1;
        IF @OwnerID <> @UserID
            THROW 50002, N'Not authorized to modify this meal plan entry', 1;

        UPDATE dbo.MealPlanEntries
        SET CategoryID  = @CategoryID,
            PlannedDate = @PlannedDate,
            Servings    = @Servings,
            Notes       = @Notes
        WHERE MealPlanEntryID = @MealPlanEntryID;

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'MEAL_PLAN_UPDATE',
            @EntityType = N'MealPlanEntry',
            @EntityID   = @MealPlanEntryID;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_UnplanMeal =====
-- Removes a planned meal.

CREATE OR ALTER PROCEDURE dbo.sp_UnplanMeal
    @MealPlanEntryID INT,
    @UserID          INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @OwnerID INT = (SELECT UserID FROM dbo.MealPlanEntries WHERE MealPlanEntryID = @MealPlanEntryID);

        IF @OwnerID IS NULL
            THROW 50003, N'Meal plan entry not found', 1;
        IF @OwnerID <> @UserID
            THROW 50002, N'Not authorized to delete this meal plan entry', 1;

        DELETE FROM dbo.MealPlanEntries WHERE MealPlanEntryID = @MealPlanEntryID;

        EXEC dbo.sp_WriteAudit
            @UserID     = @UserID,
            @Action     = N'MEAL_PLAN_DELETE',
            @EntityType = N'MealPlanEntry',
            @EntityID   = @MealPlanEntryID;

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        THROW;
    END CATCH;
END
GO

-- ===== sp_GetWeeklyPlan =====
-- 7 days starting at @StartDate. Returns entries joined to recipe + category
-- names so the weekly view can render rows directly.

CREATE OR ALTER PROCEDURE dbo.sp_GetWeeklyPlan
    @UserID    INT,
    @StartDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @EndDate DATE = DATEADD(day, 6, @StartDate);

    SELECT
        mpe.MealPlanEntryID,
        mpe.PlannedDate,
        mpe.CategoryID,
        c.Name           AS CategoryName,
        mpe.RecipeID,
        r.Title          AS RecipeTitle,
        mpe.Servings,
        mpe.Notes,
        mpe.CreatedAt
    FROM dbo.MealPlanEntries mpe
    JOIN dbo.Recipes r    ON r.RecipeID    = mpe.RecipeID
    JOIN dbo.Categories c ON c.CategoryID  = mpe.CategoryID
    WHERE mpe.UserID = @UserID
      AND mpe.PlannedDate BETWEEN @StartDate AND @EndDate
    ORDER BY mpe.PlannedDate, mpe.CategoryID, mpe.MealPlanEntryID;
END
GO

-- ===== sp_GetMonthlyPlan =====
-- All entries in a specific month (timezone-naive — uses the DB's calendar month
-- on the DATE column, which has no timezone).

CREATE OR ALTER PROCEDURE dbo.sp_GetMonthlyPlan
    @UserID INT,
    @Year   INT,
    @Month  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        mpe.MealPlanEntryID,
        mpe.PlannedDate,
        mpe.CategoryID,
        c.Name           AS CategoryName,
        mpe.RecipeID,
        r.Title          AS RecipeTitle,
        mpe.Servings,
        mpe.Notes,
        mpe.CreatedAt
    FROM dbo.MealPlanEntries mpe
    JOIN dbo.Recipes r    ON r.RecipeID    = mpe.RecipeID
    JOIN dbo.Categories c ON c.CategoryID  = mpe.CategoryID
    WHERE mpe.UserID = @UserID
      AND YEAR(mpe.PlannedDate)  = @Year
      AND MONTH(mpe.PlannedDate) = @Month
    ORDER BY mpe.PlannedDate, mpe.CategoryID, mpe.MealPlanEntryID;
END
GO
