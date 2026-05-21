namespace MealPrepApp.Models;

/// <summary>The single row from <c>sp_GetDashboardCounts</c> — the Acasa KPI tile values.</summary>
public sealed class DashboardCounts
{
    public int RecipesActiveCount { get; set; }
    public int IngredientsCount { get; set; }
    public int MealsPlannedFromTodayCount { get; set; }
    public int FavoritesCount { get; set; }
}
