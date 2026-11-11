using MealPrepApp.Models;

namespace MealPrepApp.Services;

/// <summary>Quantity-based readiness and an explicit FEFO review. Never mutates stock.</summary>
public static class InventoryCalculator
{
    public static RecipeReadiness Readiness(RecipeFull recipe, decimal servings,
        IEnumerable<InventoryBatch> batches, IEnumerable<UnitMap> unitMap, DateOnly today,
        bool includeExpired = false)
    {
        if (recipe.Servings is null or <= 0 || servings is <= 0 or > 1000 || recipe.Ingredients.Count == 0)
            return new(ReadinessStatus.NeedsReview, []);
        var factor = servings / recipe.Servings.Value;
        var map = unitMap.ToDictionary(m => m.UnitID);
        UnitMap Map(int id) => map.GetValueOrDefault(id) ?? new(id, id, 1);
        var stock = batches.Where(b => b.RemainingQuantity > 0 &&
            (includeExpired || b.ExpiresOn is null || DateOnly.FromDateTime(b.ExpiresOn.Value) >= today)).ToArray();
        var result = new List<IngredientShortage>();
        // Group duplicate lines before comparing, so the same stock is not counted twice.
        foreach (var group in recipe.Ingredients.GroupBy(i => (i.IngredientID, Map(i.UnitID).CanonicalUnitID)))
        {
            var required = group.Sum(i => i.Quantity * Map(i.UnitID).Multiplier * factor);
            var available = stock.Where(b => b.IngredientID == group.Key.IngredientID &&
                Map(b.UnitID).CanonicalUnitID == group.Key.CanonicalUnitID)
                .Sum(b => b.RemainingQuantity * Map(b.UnitID).Multiplier);
            var missing = Math.Max(0, required - available);
            var review = group.Any(i => i.Quantity <= 0) || (missing > 0 && stock.Any(b =>
                b.IngredientID == group.Key.IngredientID && Map(b.UnitID).CanonicalUnitID != group.Key.CanonicalUnitID));
            result.Add(new(group.Key.IngredientID, group.First().IngredientName, group.Key.CanonicalUnitID,
                required, available, missing, review));
        }
        return new(result.Any(i => i.NeedsReview) ? ReadinessStatus.NeedsReview :
            result.Any(i => i.Missing > 0) ? ReadinessStatus.Missing : ReadinessStatus.Ready, result);
    }

    public static ConsumptionReview ReviewConsumption(RecipeFull recipe, decimal servings,
        IEnumerable<InventoryBatch> batches, IEnumerable<UnitMap> unitMap, DateOnly today,
        bool includeExpired = false)
    {
        var stock = batches.ToArray();
        var maps = unitMap.ToDictionary(m => m.UnitID);
        UnitMap Map(int id) => maps.GetValueOrDefault(id) ?? new(id, id, 1);
        var readiness = Readiness(recipe, servings, stock, maps.Values, today, includeExpired);
        var deductions = new List<StockDeduction>();
        foreach (var ingredient in readiness.Ingredients)
        {
            var remaining = ingredient.Required;
            foreach (var batch in stock.Where(b => b.IngredientID == ingredient.IngredientID &&
                Map(b.UnitID).CanonicalUnitID == ingredient.UnitID && b.RemainingQuantity > 0 &&
                (includeExpired || b.ExpiresOn is null || DateOnly.FromDateTime(b.ExpiresOn.Value) >= today))
                .OrderBy(b => b.ExpiresOn is null).ThenBy(b => b.ExpiresOn).ThenBy(b => b.BatchID))
            {
                var take = Math.Min(batch.RemainingQuantity, remaining / Map(batch.UnitID).Multiplier);
                if (take <= 0) break;
                deductions.Add(new(batch.BatchID, batch.Version, take));
                remaining -= take * Map(batch.UnitID).Multiplier;
            }
        }
        return new(deductions, readiness);
    }
}
