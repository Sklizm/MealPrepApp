namespace MealPrepApp.Models;

/// <summary>A pantry ("Frigider") stock row from <c>sp_GetPantry</c>.</summary>
public sealed class PantryItem
{
    public int UserPantryID { get; set; }
    public int IngredientID { get; set; }
    public string IngredientName { get; set; } = "";
    public int UnitID { get; set; }
    public string UnitName { get; set; } = "";
    public string UnitAbbreviation { get; set; } = "";
    public decimal Quantity { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
