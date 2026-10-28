-- Run only in the disposable database created by scripts/test-database.sh.
USE MealPrepDB;
GO
DECLARE @Users TABLE (ID INT);
INSERT @Users EXEC dbo.sp_RegisterUser N'regression', N'regression@example.invalid', N'test-hash';
DECLARE @UserID INT = (SELECT ID FROM @Users);
DECLARE @OtherUsers TABLE (ID INT);
INSERT @OtherUsers EXEC dbo.sp_RegisterUser N'other', N'other@example.invalid', N'other-hash';
DECLARE @OtherID INT = (SELECT ID FROM @OtherUsers);
DECLARE @Recipes TABLE (ID INT);
INSERT @Recipes EXEC dbo.sp_CreateRecipe @UserID=@UserID, @Title=N'Original', @Instructions=N'Prepare';
DECLARE @RecipeID INT = (SELECT ID FROM @Recipes);
DECLARE @Version BINARY(8) = (SELECT RowVersion FROM dbo.Recipes WHERE RecipeID=@RecipeID);

EXEC dbo.sp_UpdateRecipe @RecipeID=@RecipeID, @UserID=@UserID, @CategoryID=NULL,
 @Title=N'Updated', @Description=NULL, @Instructions=N'Prepare',
 @PrepTimeMinutes=NULL, @CookTimeMinutes=NULL, @Servings=NULL, @IngredientsJson=NULL, @RowVersion=@Version;
BEGIN TRY
 EXEC dbo.sp_UpdateRecipe @RecipeID=@RecipeID, @UserID=@UserID, @CategoryID=NULL,
  @Title=N'Stale overwrite', @Description=NULL, @Instructions=N'Prepare',
  @PrepTimeMinutes=NULL, @CookTimeMinutes=NULL, @Servings=NULL, @IngredientsJson=NULL, @RowVersion=@Version;
 THROW 51000, N'Stale update unexpectedly succeeded', 1;
END TRY
BEGIN CATCH
 IF ERROR_NUMBER() <> 50004 THROW;
END CATCH;
IF (SELECT Title FROM dbo.Recipes WHERE RecipeID=@RecipeID) <> N'Updated'
 THROW 51000, N'Stale update changed data', 1;

BEGIN TRY
 EXEC dbo.sp_DeleteRecipe @RecipeID=@RecipeID, @UserID=@OtherID;
 THROW 51000, N'Non-owner delete succeeded', 1;
END TRY
BEGIN CATCH
 IF ERROR_NUMBER() <> 50002 THROW;
END CATCH;

-- Invalid ingredient FK must roll back the recipe header and audit together.
DECLARE @CountBefore INT = (SELECT COUNT(*) FROM dbo.Recipes);
BEGIN TRY
 EXEC dbo.sp_CreateRecipe @UserID=@UserID, @Title=N'Invalid', @Instructions=N'Prepare',
  @IngredientsJson=N'[{"IngredientID":-1,"UnitID":-1,"Quantity":1}]';
 THROW 51000, N'Invalid ingredient accepted', 1;
END TRY
BEGIN CATCH
 IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
IF (SELECT COUNT(*) FROM dbo.Recipes) <> @CountBefore
 THROW 51000, N'Failed creation left a header behind', 1;
IF EXISTS (SELECT 1 FROM dbo.AuditLog WHERE Details=N'Invalid')
 THROW 51000, N'Failed creation left an audit event behind', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.AuditLog WHERE UserID=@UserID AND EntityID=@RecipeID AND Action=N'RECIPE_UPDATE')
 THROW 51000, N'Missing update audit', 1;

EXEC dbo.sp_ChangePassword @UserID=@UserID, @NewPasswordHash=N'new-test-hash', @ExpectedPasswordHash=N'test-hash';
BEGIN TRY
 EXEC dbo.sp_ChangePassword @UserID=@UserID, @NewPasswordHash=N'stale-hash', @ExpectedPasswordHash=N'test-hash';
 THROW 51000, N'Stale credentials accepted', 1;
END TRY
BEGIN CATCH
 IF ERROR_NUMBER() <> 50004 THROW;
END CATCH;
BEGIN TRY
 EXEC dbo.sp_ResetForgottenPassword N'regression', N'regression@example.invalid', N'unsafe-reset';
 THROW 51000, N'Unsafe recovery accepted', 1;
END TRY
BEGIN CATCH
 IF ERROR_NUMBER() <> 50005 THROW;
END CATCH;
PRINT N'Database regression checks passed';
GO
