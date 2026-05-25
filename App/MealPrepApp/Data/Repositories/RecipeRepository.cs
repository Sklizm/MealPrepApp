using System.Data;
using System.Text.Json;
using Dapper;
using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Recipes (read + write): wraps sp_CreateRecipe, sp_UpdateRecipe, sp_DeleteRecipe,
/// sp_GetRecipeFull, sp_GetRecipes, sp_SearchRecipesByTitle, sp_FindRecipesByIngredients.</summary>
public sealed class RecipeRepository : RepositoryBase
{
    public RecipeRepository(IDbConnectionFactory factory) : base(factory) { }

    /// <summary>Creates a recipe with its ingredient lines in one transaction; returns the new RecipeID.</summary>
    public Task<int> CreateRecipeAsync(
        int userId, int? categoryId, string title, string? description, string instructions,
        int? prepTimeMinutes, int? cookTimeMinutes, int? servings,
        IReadOnlyList<RecipeIngredientInput> ingredients) =>
        ExecuteScalarProcAsync<int>("sp_CreateRecipe", new
        {
            UserID = userId,
            CategoryID = categoryId,
            Title = title,
            Description = description,
            Instructions = instructions,
            PrepTimeMinutes = prepTimeMinutes,
            CookTimeMinutes = cookTimeMinutes,
            Servings = servings,
            IngredientsJson = JsonSerializer.Serialize(ingredients)
        });

    /// <summary>
    /// Updates a recipe. Pass the <paramref name="rowVersion"/> from <see cref="GetRecipeFullAsync"/>;
    /// a stale token throws <see cref="AppDbException"/> with error 50004.
    /// <paramref name="ingredients"/> null leaves the ingredient list untouched; a non-null list
    /// (even empty) replaces it.
    /// </summary>
    public Task UpdateRecipeAsync(
        int recipeId, int userId, int? categoryId, string title, string? description, string instructions,
        int? prepTimeMinutes, int? cookTimeMinutes, int? servings,
        IReadOnlyList<RecipeIngredientInput>? ingredients, byte[] rowVersion) =>
        ExecuteProcAsync("sp_UpdateRecipe", new
        {
            RecipeID = recipeId,
            UserID = userId,
            CategoryID = categoryId,
            Title = title,
            Description = description,
            Instructions = instructions,
            PrepTimeMinutes = prepTimeMinutes,
            CookTimeMinutes = cookTimeMinutes,
            Servings = servings,
            IngredientsJson = ingredients is null ? null : JsonSerializer.Serialize(ingredients),
            RowVersion = rowVersion
        });

    public Task DeleteRecipeAsync(int recipeId, int userId) =>
        ExecuteProcAsync("sp_DeleteRecipe", new { RecipeID = recipeId, UserID = userId });

    /// <summary>Inserts or replaces the single stored photo for a recipe. Owner-only in SQL.</summary>
    public Task SetRecipePhotoAsync(int recipeId, int userId, byte[] imageData, string contentType) =>
        ExecuteProcAsync("sp_SetRecipePhoto", new
        {
            RecipeID = recipeId,
            UserID = userId,
            ImageData = imageData,
            ContentType = contentType
        });

    /// <summary>Loads the stored photo for a recipe, or null when the recipe has no photo.</summary>
    public Task<RecipePhotoData?> GetRecipePhotoAsync(int recipeId) =>
        QuerySingleOrDefaultProcAsync<RecipePhotoData>("sp_GetRecipePhoto", new { RecipeID = recipeId });

    /// <summary>Deletes the stored recipe photo. Owner-only in SQL; silent if no photo exists.</summary>
    public Task DeleteRecipePhotoAsync(int recipeId, int userId) =>
        ExecuteProcAsync("sp_DeleteRecipePhoto", new { RecipeID = recipeId, UserID = userId });

    /// <summary>Loads a recipe header + its ingredient lines, or null if not found.</summary>
    public Task<RecipeFull?> GetRecipeFullAsync(int recipeId) =>
        QueryMultipleProcAsync("sp_GetRecipeFull", new { RecipeID = recipeId }, async grid =>
        {
            var recipe = await grid.ReadSingleOrDefaultAsync<RecipeFull>();
            if (recipe is null)
                return null;

            var lines = await grid.ReadAsync<RecipeIngredientLine>();
            recipe.Ingredients = lines.AsList();
            return recipe;
        });

    public Task<IReadOnlyList<RecipeListItem>> GetRecipesAsync(
        int? userId = null, int? categoryId = null, int pageNumber = 1, int pageSize = 20) =>
        QueryProcAsync<RecipeListItem>("sp_GetRecipes",
            new { UserID = userId, CategoryID = categoryId, PageNumber = pageNumber, PageSize = pageSize });

    public Task<IReadOnlyList<RecipeListItem>> SearchRecipesByTitleAsync(
        string term, int pageNumber = 1, int pageSize = 20) =>
        QueryProcAsync<RecipeListItem>("sp_SearchRecipesByTitle",
            new { Term = term, PageNumber = pageNumber, PageSize = pageSize });

    /// <summary>"What can I make with…?" — passes the ids as the <c>dbo.IntList</c> table-valued parameter.</summary>
    public Task<IReadOnlyList<RecipeFindResult>> FindRecipesByIngredientsAsync(
        IReadOnlyCollection<int> ingredientIds, int? minMatchCount = null)
    {
        var tvp = new DataTable();
        tvp.Columns.Add("ID", typeof(int));
        foreach (var id in ingredientIds)
            tvp.Rows.Add(id);

        var parameters = new DynamicParameters();
        parameters.Add("@IngredientIDs", tvp.AsTableValuedParameter("dbo.IntList"));
        parameters.Add("@MinMatchCount", minMatchCount);

        return QueryProcAsync<RecipeFindResult>("sp_FindRecipesByIngredients", parameters);
    }
}
