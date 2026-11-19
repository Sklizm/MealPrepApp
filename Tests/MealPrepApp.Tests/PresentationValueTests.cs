using System.Globalization;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels;
using Xunit;

namespace MealPrepApp.Tests;

public sealed class PresentationValueTests
{
    [Theory]
    [InlineData("en-US", ".")]
    [InlineData("ro-RO", ",")]
    [InlineData("ru-RU", ",")]
    public void DisplayRoundsInTheRegionAndLeavesExactValuesInEditors(string region, string separator)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(region);
            Assert.Equal("1", PresentationValues.Quantity(1m));
            Assert.Equal($"1{separator}5", PresentationValues.Quantity(1.50m));
            Assert.Equal($"0{separator}33", PresentationValues.Quantity(0.3333m));
            Assert.Equal($"< 0{separator}01", PresentationValues.Quantity(0.0001m));
            Assert.Equal($"-(< 0{separator}01)", PresentationValues.Quantity(-0.0001m));
            Assert.Equal("101", PresentationValues.Calories(100.5m));
            Assert.Equal(L.Text("Value.Unknown"), PresentationValues.Quantity(null));
            var calendar = new DateTime(2030, 4, 7);
            Assert.Equal(calendar.ToString("d"), PresentationValues.Calendar(calendar));
            var utc = new DateTime(2030, 4, 7, 10, 0, 0, DateTimeKind.Utc);
            Assert.Equal(utc.ToLocalTime().ToString("g"), PresentationValues.Event(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)));

            var row = new AssistantIngredientRow { QuantityInput = $"0{separator}3333" };
            Assert.Equal(0.3333m, row.Capture().Quantity);
            row.QuantityInput = separator == "," ? "1.234" : "1,234";
            Assert.True(row.HasInvalidQuantity);
            Assert.Null(row.Capture().Quantity);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void RecipeReviewMarksEveryDuplicateAndRevalidatesAfterCorrection()
    {
        var ingredient = new Ingredient { IngredientID = 1, Name = "Rice" };
        var other = new Ingredient { IngredientID = 2, Name = "Sugar" };
        var unit = new Unit { UnitID = 1, Abbreviation = "g" };
        var recipe = new AssistantRecipeRow();
        var first = new AssistantIngredientRow { Ingredient = ingredient, Unit = unit, Quantity = 0.3333m };
        var second = new AssistantIngredientRow { Ingredient = ingredient, Unit = unit, Quantity = 1m };
        recipe.Ingredients.Add(first);
        recipe.Ingredients.Add(second);
        recipe.ValidateRows();
        Assert.Equal(2, recipe.UnresolvedRows);
        Assert.Equal("Ai.Error.DuplicateRecipeIngredient", first.Problem?.Key);
        Assert.Equal("Ai.Error.DuplicateRecipeIngredient", second.Problem?.Key);

        second.Ingredient = other;
        recipe.ValidateRows();
        Assert.Equal(0, recipe.UnresolvedRows);
        Assert.Equal(0.3333m, recipe.Capture().Ingredients[0].Quantity);
    }
}
