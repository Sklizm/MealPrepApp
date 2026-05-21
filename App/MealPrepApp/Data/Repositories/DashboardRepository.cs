using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Acasa dashboard: wraps sp_GetDashboardCounts, sp_GetRecentRecipes.</summary>
public sealed class DashboardRepository : RepositoryBase
{
    public DashboardRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<DashboardCounts> GetDashboardCountsAsync(int userId) =>
        QuerySingleProcAsync<DashboardCounts>("sp_GetDashboardCounts", new { UserID = userId });

    public Task<IReadOnlyList<RecentRecipe>> GetRecentRecipesAsync(int userId, int topN = 12) =>
        QueryProcAsync<RecentRecipe>("sp_GetRecentRecipes", new { UserID = userId, TopN = topN });
}
