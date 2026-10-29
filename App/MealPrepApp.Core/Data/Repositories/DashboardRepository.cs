using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Acasa dashboard: wraps sp_GetDashboardCounts, sp_GetRecentRecipes.</summary>
public class DashboardRepository : RepositoryBase
{
    public DashboardRepository(IDbConnectionFactory factory) : base(factory) { }

    public virtual Task<DashboardCounts> GetDashboardCountsAsync(int userId, CancellationToken cancellationToken = default) =>
        QuerySingleProcAsync<DashboardCounts>("sp_GetDashboardCounts", new { UserID = userId }, cancellationToken);

    public virtual Task<IReadOnlyList<RecentRecipe>> GetRecentRecipesAsync(int userId, int topN = 12, CancellationToken cancellationToken = default) =>
        QueryProcAsync<RecentRecipe>("sp_GetRecentRecipes", new { UserID = userId, TopN = topN }, cancellationToken);
}
