using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Meal planning: wraps sp_PlanMeal, sp_UpdatePlannedMeal, sp_UnplanMeal,
/// sp_GetWeeklyPlan, sp_GetMonthlyPlan.</summary>
public sealed class MealPlanRepository : RepositoryBase
{
    public MealPlanRepository(IDbConnectionFactory factory) : base(factory) { }

    /// <summary>Plans a recipe into a date + meal slot; returns the new MealPlanEntryID.
    /// <paramref name="servings"/> null means "use the recipe's default servings".</summary>
    public Task<int> PlanMealAsync(
        int userId, int recipeId, int categoryId, DateTime plannedDate, int? servings, string? notes) =>
        ExecuteScalarProcAsync<int>("sp_PlanMeal", new
        {
            UserID = userId,
            RecipeID = recipeId,
            CategoryID = categoryId,
            PlannedDate = plannedDate.Date,
            Servings = servings,
            Notes = notes
        });

    public Task UpdatePlannedMealAsync(
        int mealPlanEntryId, int userId, int categoryId, DateTime plannedDate, int? servings, string? notes) =>
        ExecuteProcAsync("sp_UpdatePlannedMeal", new
        {
            MealPlanEntryID = mealPlanEntryId,
            UserID = userId,
            CategoryID = categoryId,
            PlannedDate = plannedDate.Date,
            Servings = servings,
            Notes = notes
        });

    public Task UnplanMealAsync(int mealPlanEntryId, int userId) =>
        ExecuteProcAsync("sp_UnplanMeal", new { MealPlanEntryID = mealPlanEntryId, UserID = userId });

    /// <summary>Seven days of entries starting at <paramref name="startDate"/>.</summary>
    public Task<IReadOnlyList<MealPlanEntry>> GetWeeklyPlanAsync(int userId, DateTime startDate) =>
        QueryProcAsync<MealPlanEntry>("sp_GetWeeklyPlan",
            new { UserID = userId, StartDate = startDate.Date });

    /// <summary>All entries in the given calendar month.</summary>
    public Task<IReadOnlyList<MealPlanEntry>> GetMonthlyPlanAsync(int userId, int year, int month) =>
        QueryProcAsync<MealPlanEntry>("sp_GetMonthlyPlan",
            new { UserID = userId, Year = year, Month = month });
}
