USE MealPrepDB;
GO

-- MealPlanEntries: one row per (user, date, meal slot, recipe).
-- CategoryID is the meal slot (UI shows only 4 of the 6 as weekly columns).
-- PlannedDate is DATE (no wall-clock time) — week/month rollups are trivial range filters.
-- Servings NULL means "use the recipe's default servings"; the shopping list proc
-- handles this with ISNULL(mpe.Servings, 1).

IF OBJECT_ID(N'dbo.MealPlanEntries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MealPlanEntries (
        MealPlanEntryID  INT IDENTITY(1,1) NOT NULL,
        UserID           INT               NOT NULL,
        RecipeID         INT               NOT NULL,
        CategoryID       INT               NOT NULL,
        PlannedDate      DATE              NOT NULL,
        Servings         INT               NULL,
        Notes            NVARCHAR(500)     NULL,
        CreatedAt        DATETIME2(0)      NOT NULL
            CONSTRAINT DF_MealPlanEntries_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_MealPlanEntries           PRIMARY KEY CLUSTERED (MealPlanEntryID),
        CONSTRAINT FK_MealPlanEntries_Users     FOREIGN KEY (UserID)     REFERENCES dbo.Users(UserID),
        CONSTRAINT FK_MealPlanEntries_Recipes   FOREIGN KEY (RecipeID)   REFERENCES dbo.Recipes(RecipeID) ON DELETE CASCADE,
        CONSTRAINT FK_MealPlanEntries_Categories FOREIGN KEY (CategoryID) REFERENCES dbo.Categories(CategoryID),
        CONSTRAINT CK_MealPlanEntries_Servings  CHECK (Servings IS NULL OR Servings > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_MealPlanEntries_UserID_PlannedDate'
                 AND object_id = OBJECT_ID(N'dbo.MealPlanEntries'))
    CREATE INDEX IX_MealPlanEntries_UserID_PlannedDate
        ON dbo.MealPlanEntries(UserID, PlannedDate);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_MealPlanEntries_RecipeID'
                 AND object_id = OBJECT_ID(N'dbo.MealPlanEntries'))
    CREATE INDEX IX_MealPlanEntries_RecipeID
        ON dbo.MealPlanEntries(RecipeID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_MealPlanEntries_CategoryID'
                 AND object_id = OBJECT_ID(N'dbo.MealPlanEntries'))
    CREATE INDEX IX_MealPlanEntries_CategoryID
        ON dbo.MealPlanEntries(CategoryID);
GO
