USE MealPrepDB;
GO

-- RecipeFavorites: many-to-many between Users and Recipes.
-- Composite PK (UserID, RecipeID) is the natural key — a user can favorite
-- a given recipe at most once. Both FKs cascade because a favorite has
-- no meaning without its user or recipe. No multi-cascade-path issue:
-- Recipes.UserID is RESTRICT, so there's no cycle.

IF OBJECT_ID(N'dbo.RecipeFavorites', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeFavorites (
        UserID      INT NOT NULL,
        RecipeID    INT NOT NULL,
        FavoritedAt DATETIME2(0) NOT NULL
            CONSTRAINT DF_RecipeFavorites_FavoritedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RecipeFavorites           PRIMARY KEY CLUSTERED (UserID, RecipeID),
        CONSTRAINT FK_RecipeFavorites_Users     FOREIGN KEY (UserID)   REFERENCES dbo.Users(UserID)     ON DELETE CASCADE,
        CONSTRAINT FK_RecipeFavorites_Recipes   FOREIGN KEY (RecipeID) REFERENCES dbo.Recipes(RecipeID) ON DELETE CASCADE
    );
END
GO

-- Secondary index for "what users favorited this recipe?" / cascade-from-recipe perf.
-- UserID is already leading column of the PK so no separate UserID index needed.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_RecipeFavorites_RecipeID'
                 AND object_id = OBJECT_ID(N'dbo.RecipeFavorites'))
    CREATE INDEX IX_RecipeFavorites_RecipeID
        ON dbo.RecipeFavorites(RecipeID);
GO
