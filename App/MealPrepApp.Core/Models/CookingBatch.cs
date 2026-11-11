using MealPrepApp.Services;
namespace MealPrepApp.Models;
public sealed class CookingBatch : LocalizedObservableObject
{
    public decimal FreePortions=>Math.Max(0,RemainingPortions-ReservedPortions);
    [LocalizedProperty] public string PreparedSummary=>L.Text("UX.PreparedSummary",FreePortions,ExpiresOn is {} date?date.ToString("d"):L.Text("UX.UnknownExpiry"));
    [LocalizedProperty] public string PreparedLabel=>$"{RecipeTitle} · {FreePortions:0.####}";
    public int CookingBatchID { get; set; }
    public int? RecipeID { get; set; }
    public string RecipeTitle { get; set; } = "";
    public decimal PlannedPortions { get; set; }
    public decimal? ProducedPortions { get; set; }
    public decimal UsedPortions { get; set; }
    public decimal RemainingPortions { get; set; }
    public decimal ReservedPortions { get; set; }
    public DateTime? CookedAt { get; set; }
    public DateTime? PreparedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? RecipeSnapshot { get; set; }
    public string? NutritionSnapshot { get; set; }
    public long Version { get; set; }
}
public sealed record MealAllocation(DateTime PlannedDate, int CategoryID, decimal Portions, string? Notes = null);
public sealed record CookingCompletion(int CookingBatchID, long ExpectedVersion, decimal ProducedPortions,
    decimal ConsumedPortions, DateTime PreparedOn, DateTime? ExpiresOn, IReadOnlyList<StockDeduction> Deductions,
    bool IncludeExpired = false);
public sealed record ActivitySummary(int PlannedMeals, decimal PlannedPortions, decimal CookedPortions,
    decimal ConsumedPortions, decimal DiscardedPortions);
public sealed record WasteQuantity(int IngredientID, string IngredientName, int UnitID, string UnitAbbreviation, decimal Quantity);
public sealed record MealRepetition(int RecipeID, string RecipeTitle, int PlannedMeals);
public sealed record ActivityReport(ActivitySummary Summary, IReadOnlyList<WasteQuantity> Waste, IReadOnlyList<MealRepetition> Repetition);
