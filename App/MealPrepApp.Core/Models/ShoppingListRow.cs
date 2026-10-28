namespace MealPrepApp.Models;

/// <summary>A computed shopping-list row from <c>sp_GetShoppingList</c>:
/// what is needed for the planned meals, minus what is already in the pantry.
/// Only rows with <see cref="ToBuyQty"/> &gt; 0 are returned.</summary>
public sealed class ShoppingListRow
{
    public int ItemID { get; set; }
    public string CategoryName { get; set; }="Fără categorie";
    public bool IsPurchased { get; set; }
    public bool IsImported { get; set; }
    public string ImportLabel => IsImported ? "Adăugat în frigider" : "Adaugă în frigider";
    public bool CanImport => IsPurchased && !IsImported;
    public int IngredientID { get; set; }
    public string IngredientName { get; set; } = "";
    public int UnitID { get; set; }
    public string UnitAbbreviation { get; set; } = "";
    public string UnitName { get; set; } = "";
    public decimal NeededQty { get; set; }
    public decimal OnHandQty { get; set; }
    public decimal ToBuyQty { get; set; }
}

public sealed class ShoppingSnapshot
{
    public int SnapshotID { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Label => $"{DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc).ToLocalTime():dd.MM.yyyy HH:mm} • {StartDate:dd.MM} – {EndDate:dd.MM}";
}
