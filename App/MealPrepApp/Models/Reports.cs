namespace MealPrepApp.Models;

/// <summary>The single row from <c>sp_GetMonthlyStats</c> — headline numbers for the
/// "Statistici lunare" sub-tab of Rapoarte.</summary>
public sealed class MonthlyStats
{
    public int TotalMealsPlanned { get; set; }
    public int BreakfastCount { get; set; }
    public int LunchCount { get; set; }
    public int DinnerCount { get; set; }
    public int SnackCount { get; set; }
    public int DessertCount { get; set; }
    public int DrinkCount { get; set; }
    public int DistinctRecipes { get; set; }
    public int DistinctIngredients { get; set; }
}

/// <summary>Row from <c>sp_GetTopRecipes</c> — most-planned recipes in a month.</summary>
public sealed class TopRecipe
{
    public int RecipeID { get; set; }
    public string Title { get; set; } = "";
    public string? CategoryName { get; set; }
    public int PlannedCount { get; set; }
}

/// <summary>Row from <c>sp_GetTopIngredients</c> — most-used ingredients in a month.</summary>
public sealed class TopIngredient
{
    public int IngredientID { get; set; }
    public string Name { get; set; } = "";
    public int UsageCount { get; set; }
}
