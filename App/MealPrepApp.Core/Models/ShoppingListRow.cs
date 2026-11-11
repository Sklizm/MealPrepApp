using MealPrepApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
namespace MealPrepApp.Models;

/// <summary>A computed shopping-list row from <c>sp_GetShoppingList</c>:
/// what is needed for the planned meals, minus what is already in the pantry.
/// Only rows with <see cref="ToBuyQty"/> &gt; 0 are returned.</summary>
public sealed partial class ShoppingListRow : LocalizedObservableObject
{
    public int ItemID { get; set; }
    public string CategoryName { get; set; }="Fără categorie";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanImport))] [NotifyPropertyChangedFor(nameof(PurchasedSummary))] private bool _isPurchased;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanImport))] [NotifyPropertyChangedFor(nameof(ImportLabel))] private bool _isImported;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanImport))] private bool _isSaving;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanImport))] private bool _saveFailed;
    public int SnapshotID { get; set; }
    public long Version { get; set; } = 1;
    public decimal? PurchasedQuantity { get; set; }
    public int? PurchasedUnitID { get; set; }
    public string PurchasedUnitAbbreviation { get; set; } = "";
    public DateTime? PurchasedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public bool IsManual { get; set; }
    public bool IsDeleted { get; set; }
    public decimal ActualQuantity => PurchasedQuantity ?? ToBuyQty;
    public string ActualUnit => string.IsNullOrEmpty(PurchasedUnitAbbreviation) ? UnitAbbreviation : PurchasedUnitAbbreviation;
    [ObservableProperty] private bool _hasConflict;
    [LocalizedProperty] public string RequiredSummary => L.Text("UX.RequiredSummary", ToBuyQty, UnitAbbreviation);
    [LocalizedProperty] public string PurchasedSummary => IsPurchased ? L.Text("UX.PurchasedSummary", ActualQuantity, ActualUnit) : "";
    public bool HasMapping => IngredientID.HasValue && (PurchasedUnitID ?? UnitID).HasValue;
    [LocalizedProperty]
    public string ImportLabel => IsImported ? L.Text("Text.AddedToPantry") : L.Text("Text.AddToPantry");
    public bool CanImport => IsPurchased && !IsImported && !IsSaving && !SaveFailed && HasMapping;
    public int? IngredientID { get; set; }
    public string IngredientName { get; set; } = "";
    public int? UnitID { get; set; }
    public string UnitAbbreviation { get; set; } = "";
    public string UnitName { get; set; } = "";
    public decimal NeededQty { get; set; }
    public decimal OnHandQty { get; set; }
    public decimal ToBuyQty { get; set; }
}

public sealed class ShoppingSnapshot : LocalizedObservableObject
{
    public int SnapshotID { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    [LocalizedProperty]
    public string Label => $"{DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc).ToLocalTime():g} • {StartDate:d} – {EndDate:d}";
}

public sealed record ShoppingChange(int SnapshotID, int? ItemID, long ExpectedVersion, string Name,
    int? IngredientID, int? UnitID, decimal RequiredQuantity, bool IsPurchased,
    decimal? PurchasedQuantity, int? PurchasedUnitID, DateTime? PurchasedOn, DateTime? ExpiresOn, bool IsDeleted = false)
{
    public static ShoppingChange From(ShoppingListRow row) => new(row.SnapshotID, row.ItemID, row.Version,
        row.IngredientName, row.IngredientID, row.UnitID, row.ToBuyQty, row.IsPurchased,
        row.PurchasedQuantity, row.PurchasedUnitID, row.PurchasedOn, row.ExpiresOn, row.IsDeleted);
}
