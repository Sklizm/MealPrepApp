namespace MealPrepApp.Models;

/// <summary>Optional nutrition values for one ingredient, normalized to a basis amount/unit.</summary>
public sealed class IngredientNutrition
{
    public int IngredientID { get; set; }
    public string IngredientName { get; set; } = "";
    public decimal BasisQuantity { get; set; } = 100;
    public int BasisUnitID { get; set; }
    public string BasisUnitName { get; set; } = "";
    public string BasisUnitAbbreviation { get; set; } = "";
    public decimal Calories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatGrams { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Calculated recipe nutrition totals and per-serving values from sp_GetRecipeNutrition.</summary>
public sealed class RecipeNutritionSummary
{
    public int RecipeID { get; set; }
    public int? Servings { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProteinGrams { get; set; }
    public decimal TotalCarbsGrams { get; set; }
    public decimal TotalFatGrams { get; set; }
    public decimal? CaloriesPerServing { get; set; }
    public decimal? ProteinGramsPerServing { get; set; }
    public decimal? CarbsGramsPerServing { get; set; }
    public decimal? FatGramsPerServing { get; set; }
    public int MissingNutritionCount { get; set; }
    public int UnconvertibleIngredientCount { get; set; }

    public bool HasCompleteNutrition => MissingNutritionCount == 0 && UnconvertibleIngredientCount == 0;
}
