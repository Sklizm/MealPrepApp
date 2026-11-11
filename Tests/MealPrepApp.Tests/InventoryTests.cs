using MealPrepApp.Models;
using MealPrepApp.Services;
using Xunit;

namespace MealPrepApp.Tests;
public sealed class InventoryTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);
    private static readonly UnitMap[] Maps = [new(1, 1, 1), new(2, 1, 1000), new(3, 3, 1), new(4, 3, 1000), new(5, 5, 1)];
    private static RecipeFull Recipe(params RecipeIngredientLine[] lines) => new() { Servings = 2, Ingredients = [.. lines] };
    private static RecipeIngredientLine Line(int ingredient, int unit, decimal qty) => new() { IngredientID = ingredient, UnitID = unit, Quantity = qty };
    private static InventoryBatch Batch(int id, int ingredient, int unit, decimal qty, DateTime? expires = null) => new() { BatchID = id, IngredientID = ingredient, UnitID = unit, RemainingQuantity = qty, ExpiresOn = expires, Version = 3 };

    [Fact] public void ReadinessScalesAndAggregatesCompatibleStock()
    {
        var recipe = Recipe(Line(1, 1, 200), Line(1, 2, .1m));
        var result = InventoryCalculator.Readiness(recipe, 4, [Batch(1, 1, 2, .5m), Batch(2, 1, 1, 99)], Maps, Today);
        Assert.Equal(ReadinessStatus.Missing, result.Status);
        Assert.Equal(600, Assert.Single(result.Ingredients).Required);
        Assert.Equal(1, result.Ingredients[0].Missing);
    }
    [Fact] public void ExpiredStockExcludedUnknownExpiryAndTodayEligible()
    {
        var recipe = Recipe(Line(1, 1, 100));
        InventoryBatch[] stock = [Batch(1, 1, 1, 1000, new(2026, 11, 10)), Batch(2, 1, 1, 40), Batch(3, 1, 1, 50, new(2026, 11, 11))];
        var result = InventoryCalculator.Readiness(recipe, 2, stock, Maps, Today);
        Assert.Equal(10, result.Ingredients[0].Missing);
        Assert.Equal(ReadinessStatus.Ready, InventoryCalculator.Readiness(recipe, 2, stock, Maps, Today, true).Status);
    }
    [Fact] public void IncompatiblePiecesAndVolumeNeverBecomeMass()
    {
        var result = InventoryCalculator.Readiness(Recipe(Line(1, 1, 200)), 2,
            [Batch(1, 1, 5, 5), Batch(2, 1, 4, 1)], Maps, Today);
        Assert.Equal(ReadinessStatus.NeedsReview, result.Status);
        Assert.Equal(0, result.Ingredients[0].Available);
    }
    [Fact] public void MissingServingBasisRequiresReview()
    {
        var recipe = Recipe(Line(1, 1, 100)); recipe.Servings = null;
        Assert.Equal(ReadinessStatus.NeedsReview, InventoryCalculator.Readiness(recipe, 2, [], Maps, Today).Status);
        Assert.Equal(ReadinessStatus.NeedsReview, InventoryCalculator.Readiness(Recipe(), 2, [], Maps, Today).Status);
    }
    [Fact] public void ConsumptionReviewUsesEarliestExpiryAndRetainsVersionsWithoutMutation()
    {
        InventoryBatch[] stock = [Batch(1, 1, 1, 100), Batch(2, 1, 2, .1m, new(2026, 11, 13)), Batch(3, 1, 1, 50, new(2026, 11, 12)), Batch(4, 1, 1, 1000, new(2026, 11, 10))];
        var review = InventoryCalculator.ReviewConsumption(Recipe(Line(1, 1, 200)), 2, stock, Maps, Today);
        Assert.Equal(new[] { 3, 2, 1 }, review.Deductions.Select(d => d.BatchID));
        Assert.Equal(new[] { 50m, .1m, 50m }, review.Deductions.Select(d => d.Quantity));
        Assert.All(review.Deductions, d => Assert.Equal(3, d.ExpectedVersion));
        Assert.Equal(100, stock[0].RemainingQuantity);
    }
}

public sealed class PlannerPreferenceTests
{
    [Fact] public void ExplicitPlannerModeSurvivesResizeAndRestart() {
        var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());var path=Path.Combine(directory,"preferences.json");
        try {
            var preferences=new PresentationPreferences(path);
            using var services=RedesignSamples.Services(configure:s=>Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(s,preferences));using var scope=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(services);
            var weekly=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<MealPrepApp.ViewModels.Planificare.PlanificareWeeklyViewModel>(scope.ServiceProvider);
            weekly.ApplyWidth(600);Assert.True(weekly.AgendaView);Assert.Null(preferences.PlannerAgenda);
            weekly.AgendaView=false;weekly.ApplyWidth(500);Assert.False(weekly.AgendaView);Assert.False(new PresentationPreferences(path).PlannerAgenda);
            weekly.AgendaView=true;weekly.ApplyWidth(1400);Assert.True(weekly.AgendaView);Assert.True(new PresentationPreferences(path).PlannerAgenda);
            weekly.AutoViewCommand.Execute(null);Assert.False(weekly.AgendaView);Assert.Null(preferences.PlannerAgenda);
        }finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
