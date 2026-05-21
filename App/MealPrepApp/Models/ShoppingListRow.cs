namespace MealPrepApp.Models;

/// <summary>A computed shopping-list row from <c>sp_GetShoppingList</c>:
/// what is needed for the planned meals, minus what is already in the pantry.
/// Only rows with <see cref="ToBuyQty"/> &gt; 0 are returned.</summary>
public sealed class ShoppingListRow
{
    public int IngredientID { get; set; }
    public string IngredientName { get; set; } = "";
    public int UnitID { get; set; }
    public string UnitAbbreviation { get; set; } = "";
    public string UnitName { get; set; } = "";
    public decimal NeededQty { get; set; }
    public decimal OnHandQty { get; set; }
    public decimal ToBuyQty { get; set; }
}
