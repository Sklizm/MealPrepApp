namespace MealPrepApp.Models;

/// <summary>Row from <c>sp_GetUnits</c>. <see cref="UnitType"/> is one of weight / volume / count.</summary>
public sealed class Unit
{
    public int UnitID { get; set; }
    public string Name { get; set; } = "";
    public string Abbreviation { get; set; } = "";
    public string UnitType { get; set; } = "";
}

/// <summary>Row from <c>sp_GetCategories</c>. Categories double as meal slots in the planner.</summary>
public sealed class Category
{
    public int CategoryID { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

/// <summary>Row from <c>sp_GetIngredientCategories</c> — the Ingrediente sidebar grouping.</summary>
public sealed class IngredientCategory
{
    public int IngredientCategoryID { get; set; }
    public string Name { get; set; } = "";
}
