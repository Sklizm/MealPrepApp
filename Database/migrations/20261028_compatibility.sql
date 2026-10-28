-- Run from Database/ with sqlcmd -b after taking a verified database backup.
-- Applies the compatibility delta only; does not rerun seeds or change login passwords.
:On Error exit
USE MealPrepDB;
GO
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 30000;
IF OBJECT_ID(N'dbo.Recipes', N'U') IS NULL
    OR OBJECT_ID(N'dbo.RecipePhotos', N'U') IS NULL
    OR OBJECT_ID(N'dbo.PasswordHistory', N'U') IS NULL
    OR OBJECT_ID(N'dbo.RecipeFavorites', N'U') IS NULL
    OR OBJECT_ID(N'dbo.UserPantry', N'U') IS NULL
    THROW 51000, N'Expected MealPrepApp baseline schema is missing. No migration applied.', 1;
IF DATABASE_PRINCIPAL_ID(N'mealprep_app_role') IS NULL
    OR DATABASE_PRINCIPAL_ID(N'mealprep_app') IS NULL
    THROW 51000, N'Expected application database user/role is missing. No migration applied.', 1;
BEGIN TRANSACTION;
GO
:r 16_recipe_photos.sql
:r procs/01_users.sql
:r procs/02_recipes_write.sql
:r procs/03_recipes_read.sql
:r procs/08_pantry.sql
:r procs/09_shopping_list.sql
:r procs/13_recipe_photos.sql
DENY EXECUTE ON OBJECT::dbo.sp_ResetForgottenPassword TO mealprep_app_role;
GO
IF OBJECT_ID(N'dbo.sp_BrowseRecipes', N'P') IS NULL
    OR OBJECT_ID(N'dbo.sp_GetPasswordHistory', N'P') IS NULL
    OR COL_LENGTH(N'dbo.RecipePhotos', N'ThumbnailData') IS NULL
    THROW 51000, N'Compatibility schema verification failed.', 1;
-- Exercise the new procedure under the actual app user's permissions without showing data.
EXECUTE AS USER = N'mealprep_app';
EXEC dbo.sp_BrowseRecipes @UserID = 0, @Mode = N'Favorite', @PageSize = 1;
REVERT;
COMMIT TRANSACTION;
GO
PRINT N'Compatibility database update applied and verified.';
