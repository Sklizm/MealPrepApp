USE MealPrepDB;
GO

-- RecipeDrafts: a partially-complete recipe a user saved from the editor to finish later.
-- Unlike dbo.Recipes, every content column is NULLable and there are no CHECK constraints,
-- because a draft may be incomplete (no title, no instructions, half-filled ingredient rows).
-- The ingredient lines are kept as an opaque JSON blob (the app serializes/deserializes them);
-- the DB never parses it. A draft belongs to one user and dies with that user.

IF OBJECT_ID(N'dbo.RecipeDrafts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeDrafts (
        DraftID          INT IDENTITY(1,1) NOT NULL,
        UserID           INT               NOT NULL,
        CategoryID       INT               NULL,
        Title            NVARCHAR(150)     NULL,
        Description      NVARCHAR(MAX)     NULL,
        Instructions     NVARCHAR(MAX)     NULL,
        PrepTimeMinutes  INT               NULL,
        CookTimeMinutes  INT               NULL,
        Servings         INT               NULL,
        IngredientsJson  NVARCHAR(MAX)     NULL,
        UpdatedAt        DATETIME2(0)      NOT NULL
            CONSTRAINT DF_RecipeDrafts_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RecipeDrafts            PRIMARY KEY CLUSTERED (DraftID),
        CONSTRAINT FK_RecipeDrafts_Users      FOREIGN KEY (UserID)     REFERENCES dbo.Users(UserID)         ON DELETE CASCADE,
        CONSTRAINT FK_RecipeDrafts_Categories FOREIGN KEY (CategoryID) REFERENCES dbo.Categories(CategoryID)
    );
END
GO

-- Drafts are always listed per user, newest first.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_RecipeDrafts_UserID'
                 AND object_id = OBJECT_ID(N'dbo.RecipeDrafts'))
    CREATE INDEX IX_RecipeDrafts_UserID
        ON dbo.RecipeDrafts(UserID);
GO
