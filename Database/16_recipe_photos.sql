USE MealPrepDB;
GO

-- RecipePhotos: one optional photo per recipe (1:1). The image bytes live in the DB so they
-- travel with it across machines, consistent with the proc-only model. The app downscales +
-- re-encodes to JPEG before storing, so rows stay small. RecipeID is both PK and FK — a recipe
-- has at most one photo, and the photo is cascade-deleted with its recipe.

IF OBJECT_ID(N'dbo.RecipePhotos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipePhotos (
        RecipeID     INT             NOT NULL,
        ImageData    VARBINARY(MAX)  NOT NULL,
        ContentType  NVARCHAR(100)   NOT NULL,
        UpdatedAt    DATETIME2(0)    NOT NULL
            CONSTRAINT DF_RecipePhotos_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RecipePhotos          PRIMARY KEY CLUSTERED (RecipeID),
        CONSTRAINT FK_RecipePhotos_Recipes  FOREIGN KEY (RecipeID) REFERENCES dbo.Recipes(RecipeID) ON DELETE CASCADE
    );
END
GO
