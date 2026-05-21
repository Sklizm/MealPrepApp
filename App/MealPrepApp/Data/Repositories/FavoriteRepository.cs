using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Favorites: wraps sp_ToggleFavorite, sp_GetFavoriteRecipes.</summary>
public sealed class FavoriteRepository : RepositoryBase
{
    public FavoriteRepository(IDbConnectionFactory factory) : base(factory) { }

    /// <summary>Toggles the favorite state. Returns true if the recipe is now favorited,
    /// false if it was just un-favorited.</summary>
    public Task<bool> ToggleFavoriteAsync(int userId, int recipeId) =>
        ExecuteScalarProcAsync<bool>("sp_ToggleFavorite", new { UserID = userId, RecipeID = recipeId });

    /// <summary>The user's favorited recipes; same row shape as <c>sp_GetRecipes</c> plus FavoritedAt.</summary>
    public Task<IReadOnlyList<RecipeListItem>> GetFavoriteRecipesAsync(
        int userId, int pageNumber = 1, int pageSize = 20) =>
        QueryProcAsync<RecipeListItem>("sp_GetFavoriteRecipes",
            new { UserID = userId, PageNumber = pageNumber, PageSize = pageSize });
}
