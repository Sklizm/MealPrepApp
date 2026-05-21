using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Rapoarte: wraps sp_GetMonthlyStats, sp_GetTopRecipes, sp_GetTopIngredients.</summary>
public sealed class ReportRepository : RepositoryBase
{
    public ReportRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<MonthlyStats> GetMonthlyStatsAsync(int userId, int year, int month) =>
        QuerySingleProcAsync<MonthlyStats>("sp_GetMonthlyStats",
            new { UserID = userId, Year = year, Month = month });

    public Task<IReadOnlyList<TopRecipe>> GetTopRecipesAsync(int userId, int year, int month, int topN = 5) =>
        QueryProcAsync<TopRecipe>("sp_GetTopRecipes",
            new { UserID = userId, Year = year, Month = month, TopN = topN });

    public Task<IReadOnlyList<TopIngredient>> GetTopIngredientsAsync(int userId, int year, int month, int topN = 10) =>
        QueryProcAsync<TopIngredient>("sp_GetTopIngredients",
            new { UserID = userId, Year = year, Month = month, TopN = topN });
}
