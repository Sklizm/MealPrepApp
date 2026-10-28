-- Verify the compatibility migration preserved the populated predecessor fixture.
USE MealPrepDB;
GO
IF (SELECT COUNT(*) FROM dbo.Users) <> 2
    THROW 51000, N'Upgrade changed user count', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username=N'regression' AND PasswordHash=N'new-test-hash')
    THROW 51000, N'Upgrade changed account credentials', 1;
IF (SELECT COUNT(*) FROM dbo.Recipes) <> 1
    OR NOT EXISTS (SELECT 1 FROM dbo.Recipes WHERE Title=N'Updated')
    THROW 51000, N'Upgrade changed existing recipes', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.RecipePhotos WHERE ImageData=0x01020304 AND ContentType=N'image/jpeg')
    THROW 51000, N'Upgrade lost the existing photo', 1;
IF COL_LENGTH(N'dbo.RecipePhotos', N'ThumbnailData') IS NULL
    OR OBJECT_ID(N'dbo.sp_BrowseRecipes', N'P') IS NULL
    THROW 51000, N'Upgrade did not add compatibility objects', 1;
PRINT N'Populated-database compatibility upgrade checks passed';
GO
