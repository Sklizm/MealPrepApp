USE MealPrepDB;
GO

IF OBJECT_ID(N'dbo.Recipes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Recipes (
        RecipeID         INT IDENTITY(1,1) NOT NULL,
        UserID           INT               NOT NULL,
        CategoryID       INT               NULL,
        Title            NVARCHAR(150)     NOT NULL,
        Description      NVARCHAR(MAX)     NULL,
        Instructions     NVARCHAR(MAX)     NOT NULL,
        PrepTimeMinutes  INT               NULL,
        CookTimeMinutes  INT               NULL,
        Servings         INT               NULL,
        CreatedAt        DATETIME2(0)      NOT NULL CONSTRAINT DF_Recipes_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2(0)      NULL,
        CONSTRAINT PK_Recipes            PRIMARY KEY CLUSTERED (RecipeID),
        CONSTRAINT FK_Recipes_Users      FOREIGN KEY (UserID)     REFERENCES dbo.Users(UserID),
        CONSTRAINT FK_Recipes_Categories FOREIGN KEY (CategoryID) REFERENCES dbo.Categories(CategoryID),
        CONSTRAINT CK_Recipes_PrepTime   CHECK (PrepTimeMinutes IS NULL OR PrepTimeMinutes >= 0),
        CONSTRAINT CK_Recipes_CookTime   CHECK (CookTimeMinutes IS NULL OR CookTimeMinutes >= 0),
        CONSTRAINT CK_Recipes_Servings   CHECK (Servings IS NULL OR Servings > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Recipes_UserID' AND object_id = OBJECT_ID(N'dbo.Recipes'))
    CREATE INDEX IX_Recipes_UserID     ON dbo.Recipes(UserID);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Recipes_CategoryID' AND object_id = OBJECT_ID(N'dbo.Recipes'))
    CREATE INDEX IX_Recipes_CategoryID ON dbo.Recipes(CategoryID);
GO
