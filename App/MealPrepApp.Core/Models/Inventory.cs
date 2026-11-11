namespace MealPrepApp.Models;

public sealed class InventoryBatch
{
    public int BatchID { get; set; }
    public int IngredientID { get; set; }
    public string IngredientName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int UnitID { get; set; }
    public string UnitAbbreviation { get; set; } = "";
    public decimal RemainingQuantity { get; set; }
    // SQL DATE values have no time zone. Convert with DateOnly.FromDateTime at boundaries.
    public DateTime? PurchasedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public DateTime? SourceAddedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long Version { get; set; }
}

public sealed record UnitMap(int UnitID, int CanonicalUnitID, decimal Multiplier);
public enum ReadinessStatus { Ready, Missing, NeedsReview }
public sealed record IngredientShortage(int IngredientID, string Name, int UnitID,
    decimal Required, decimal Available, decimal Missing, bool NeedsReview);
public sealed record RecipeReadiness(ReadinessStatus Status, IReadOnlyList<IngredientShortage> Ingredients);
public sealed record StockDeduction(int BatchID, long ExpectedVersion, decimal Quantity);
public sealed record ConsumptionReview(IReadOnlyList<StockDeduction> Deductions, RecipeReadiness Readiness);
public sealed record MutationResult(int Id, long Version, int? EventId = null);
