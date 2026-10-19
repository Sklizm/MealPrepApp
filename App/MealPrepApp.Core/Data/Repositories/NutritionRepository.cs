using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Nutrition procs: ingredient nutrition upsert/read/delete and recipe nutrition calculation.</summary>
public sealed class NutritionRepository : RepositoryBase
{
    public NutritionRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IngredientNutrition?> GetIngredientNutritionAsync(int ingredientId) =>
        QuerySingleOrDefaultProcAsync<IngredientNutrition>("sp_GetIngredientNutrition", new { IngredientID = ingredientId });

    public Task SetIngredientNutritionAsync(
        int ingredientId,
        decimal basisQuantity,
        int basisUnitId,
        decimal calories,
        decimal proteinGrams,
        decimal carbsGrams,
        decimal fatGrams) =>
        ExecuteProcAsync("sp_SetIngredientNutrition", new
        {
            IngredientID = ingredientId,
            BasisQuantity = basisQuantity,
            BasisUnitID = basisUnitId,
            Calories = calories,
            ProteinGrams = proteinGrams,
            CarbsGrams = carbsGrams,
            FatGrams = fatGrams,
        });

    public Task DeleteIngredientNutritionAsync(int ingredientId) =>
        ExecuteProcAsync("sp_DeleteIngredientNutrition", new { IngredientID = ingredientId });

    public Task<RecipeNutritionSummary?> GetRecipeNutritionAsync(int recipeId) =>
        QuerySingleOrDefaultProcAsync<RecipeNutritionSummary>("sp_GetRecipeNutrition", new { RecipeID = recipeId });
}
