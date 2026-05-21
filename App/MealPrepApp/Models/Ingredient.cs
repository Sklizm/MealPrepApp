namespace MealPrepApp.Models;

/// <summary>Row from <c>sp_GetIngredients</c> — ingredient with its default unit and category.</summary>
public sealed class Ingredient
{
    public int IngredientID { get; set; }
    public string Name { get; set; } = "";
    public int? DefaultUnitID { get; set; }
    public string? DefaultUnitName { get; set; }
    public string? DefaultUnitAbbreviation { get; set; }
    public int? IngredientCategoryID { get; set; }
    public string? IngredientCategoryName { get; set; }
}

/// <summary>Lightweight row from <c>sp_SearchIngredients</c> — drives editor autocomplete.</summary>
public sealed class IngredientSearchResult
{
    public int IngredientID { get; set; }
    public string Name { get; set; } = "";
    public int? DefaultUnitID { get; set; }
    public string? DefaultUnitAbbreviation { get; set; }
}

/// <summary>Row from <c>sp_GetIngredientUsage</c> — how many recipes reference an ingredient.
/// The app checks this before a delete so it can show a friendly "in use" message.</summary>
public sealed class IngredientUsage
{
    public int IngredientID { get; set; }
    public string Name { get; set; } = "";
    public int RecipeCount { get; set; }
}
