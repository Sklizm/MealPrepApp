using MealPrepApp.Models;
namespace MealPrepApp.Services;
public static class ServingCalculator
{
    public static decimal Factor(int? baseServings, decimal servings)
    {
        if (baseServings is null or <= 0) throw new InvalidOperationException(L.Text("Text.ScalingRequiresAValidBaseServingCount"));
        if (servings <= 0 || servings > 1000) throw new ArgumentOutOfRangeException(nameof(servings));
        return servings / baseServings.Value;
    }
    public static RecipeNutritionSummary Scale(RecipeNutritionSummary value, decimal factor) => new()
    {
        RecipeID=value.RecipeID, Servings=value.Servings,
        TotalCalories=value.TotalCalories*factor, TotalProteinGrams=value.TotalProteinGrams*factor,
        TotalCarbsGrams=value.TotalCarbsGrams*factor, TotalFatGrams=value.TotalFatGrams*factor,
        MissingNutritionCount=value.MissingNutritionCount, UnconvertibleIngredientCount=value.UnconvertibleIngredientCount
    };
}
