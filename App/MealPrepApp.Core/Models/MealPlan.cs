namespace MealPrepApp.Models;

/// <summary>A planned meal from <c>sp_GetWeeklyPlan</c> / <c>sp_GetMonthlyPlan</c>.
/// <see cref="CategoryID"/> is the meal slot. <see cref="Servings"/> null means "use the recipe default".</summary>
public sealed class MealPlanEntry
{
    public int MealPlanEntryID { get; set; }
    public DateTime PlannedDate { get; set; }
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = "";
    public int RecipeID { get; set; }
    public string RecipeTitle { get; set; } = "";
    public int? Servings { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
