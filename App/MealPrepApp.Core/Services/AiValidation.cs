using MealPrepApp.Models;

namespace MealPrepApp.Services;

public static class AiValidation
{
    public static readonly string[] Tasks = ["suggest", "import", "generate", "plan", "recipe-photo", "receipt"];
    public static bool Quantity(decimal value) => value is > 0 and <= 99999999 && decimal.Round(value, 4) == value;
    public static void Request(AiRequest value)
    {
        if (value.OperationId == Guid.Empty || !Tasks.Contains(value.Kind) || value.Language is not ("en" or "ro" or "ru") ||
            value.Prompt is null || value.Prompt.Length > 4000 || value.Source?.Length > 200000 ||
            value.Preferences is not { Servings: > 0 and <= 1000, MaxMinutes: > 0 and <= 10080, MaxRepetitions: >= 1 and <= 7 } ||
            (value.Preferences.ExcludedIngredientIds?.Length ?? 0) > 300 || (value.Preferences.MealSlotIds?.Length ?? 0) > 12 ||
            (value.ReplaceEntryIds?.Length ?? 0) > 100 || (value.PinnedMeals?.Length ?? 0) > 84 ||
            value.StartDate < new DateOnly(2000, 1, 1) || value.StartDate > new DateOnly(2100, 1, 1))
            throw new InvalidDataException("InvalidInput");
        if (value.Kind is "recipe-photo" or "receipt" && value.UploadId is null) throw new InvalidDataException("ImageRequired");
        if (value.Kind == "import" && string.IsNullOrWhiteSpace(value.Source)) throw new InvalidDataException("SourceRequired");
    }

    public static AiProposal Proposal(AiProposal value, AiWork work)
    {
        if (value.Suggestions is null || value.Recipes is null || value.Meals is null || value.Purchases is null || value.Warnings is null ||
            value.Suggestions.Length > 30 || value.Recipes.Length > 21 || value.Meals.Length > 84 || value.Purchases.Length > 200 || value.Warnings.Length > 30)
            throw new InvalidDataException("InvalidModelOutput");
        var request = work.Request; var context = work.Context;
        if (request.Kind == "plan" && request.PinnedMeals is {Length:>0} pins)
        {
            if (pins.Any(p => p.DraftIndex.HasValue)) throw new InvalidDataException("ReviewRecipesFirst");
            var meals=value.Meals.ToList();
            foreach(var pin in pins)
            {
                var existing=meals.FindIndex(m=>m.Date==pin.Date&&m.SlotId==pin.SlotId);
                if(existing>=0&&meals[existing]!=pin)throw new InvalidDataException("PinnedMealChanged");
                if(existing<0)meals.Add(pin);
            }
            value=value with {Meals=meals.ToArray()};
        }
        var excluded = (request.Preferences.ExcludedIngredientIds ?? []).ToHashSet();
        var candidates = context.Candidates.ToDictionary(c => c.RecipeId);
        bool Allowed(AiCandidate c) => !c.Ingredients.Any(i => excluded.Contains(i.IngredientID)) &&
            c.PrepMinutes.HasValue && c.CookMinutes.HasValue && c.PrepMinutes + c.CookMinutes <= request.Preferences.MaxMinutes;
        foreach (var suggestion in value.Suggestions)
            if (!candidates.TryGetValue(suggestion.RecipeId, out var candidate) || !Allowed(candidate) || suggestion.Reason.Length > 2000)
                throw new InvalidDataException("InvalidModelOutput");
        if (value.Suggestions.Select(s => s.RecipeId).Distinct().Count() != value.Suggestions.Length) throw new InvalidDataException("InvalidModelOutput");
        if (request.Kind == "suggest" && (value.Recipes.Length + value.Meals.Length + value.Purchases.Length > 0) ||
            request.Kind is "import" or "generate" or "recipe-photo" && (value.Recipes.Length != 1 || value.Meals.Length + value.Suggestions.Length + value.Purchases.Length > 0) ||
            request.Kind == "receipt" && (value.Recipes.Length + value.Meals.Length + value.Suggestions.Length > 0) ||
            request.Kind == "plan" && (value.Purchases.Length + value.Suggestions.Length > 0 || !request.AllowNewRecipes && value.Recipes.Length > 0))
            throw new InvalidDataException("InvalidModelOutput");
        var recipes = value.Recipes.Select(recipe => ValidateRecipe(recipe, context, excluded, request.Kind is "import" or "recipe-photo")).ToArray();
        if(request.Kind is "generate" or "plan" && recipes.Any(r=>r.PrepMinutes is null||r.CookMinutes is null||r.PrepMinutes+r.CookMinutes>request.Preferences.MaxMinutes))throw new InvalidDataException("InvalidModelOutput");
        var occupied = context.Plan.Where(p => !(request.ReplaceEntryIds ?? []).Contains(p.MealPlanEntryID))
            .Select(p => (DateOnly.FromDateTime(p.PlannedDate), p.CategoryID)).ToHashSet();
        var seen = new HashSet<(DateOnly, int)>();
        foreach (var meal in value.Meals)
        {
            if (meal.Date < request.StartDate || meal.Date >= request.StartDate.AddDays(7) || !Quantity(meal.Portions) || meal.Portions > 1000 ||
                !context.Categories.Any(c => c.CategoryID == meal.SlotId) || !(request.Preferences.MealSlotIds ?? context.Categories.Select(c => c.CategoryID).ToArray()).Contains(meal.SlotId) ||
                string.IsNullOrWhiteSpace(meal.BatchKey) || meal.BatchKey.Length > 80 || meal.Reason.Length > 2000 ||
                occupied.Contains((meal.Date, meal.SlotId)) || !seen.Add((meal.Date, meal.SlotId))) throw new InvalidDataException("InvalidModelOutput");
            if (meal.RecipeId.HasValue == meal.DraftIndex.HasValue) throw new InvalidDataException("InvalidModelOutput");
            if (meal.RecipeId is { } id && (!candidates.TryGetValue(id, out var recipe) || !Allowed(recipe))) throw new InvalidDataException("InvalidModelOutput");
            if (meal.DraftIndex is { } index && (index < 0 || index >= recipes.Length)) throw new InvalidDataException("InvalidModelOutput");
            if (meal.PreparedBatchId is { } batchId && (!request.Preferences.UseLeftovers || !context.Batches.Any(b => b.CookingBatchID == batchId && b.RecipeID == meal.RecipeId && b.CookedAt.HasValue &&
                    (b.ExpiresOn is null || DateOnly.FromDateTime(b.ExpiresOn.Value) >= meal.Date)))) throw new InvalidDataException("InvalidModelOutput");
        }
        foreach (var group in value.Meals.GroupBy(m => m.BatchKey))
            if (group.Select(m => (m.RecipeId, m.DraftIndex, m.PreparedBatchId)).Distinct().Count() != 1 ||
                group.Sum(m => m.Portions) > 1000 || !request.Preferences.UseLeftovers && group.Count() > 1)
                throw new InvalidDataException("InvalidModelOutput");
        foreach (var group in value.Meals.Where(m => m.PreparedBatchId.HasValue).GroupBy(m => m.PreparedBatchId))
        {
            var batch = context.Batches.Single(b => b.CookingBatchID == group.Key);
            if (group.Sum(m => m.Portions) > batch.RemainingPortions - batch.ReservedPortions) throw new InvalidDataException("InvalidModelOutput");
        }
        foreach (var group in value.Meals.GroupBy(m => (m.RecipeId, m.DraftIndex)))
            if (group.Count() > request.Preferences.MaxRepetitions) throw new InvalidDataException("InvalidModelOutput");
        var purchases = value.Purchases.Select(p => p with {
            IngredientId = p.IngredientId is {} id && context.Ingredients.Any(i => i.IngredientID == id && string.Equals(i.Name, p.Name, StringComparison.OrdinalIgnoreCase)) ? id : null,
            UnitId = p.UnitId is {} unit && context.Units.Any(u => u.UnitID == unit) ? unit : null,
            Quantity = p.Quantity is {} qty && Quantity(qty) ? qty : null,
            ExpiresOn = null // Receipts do not establish food expiry.
        }).ToArray();
        if (purchases.Any(p => string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 150 || p.Original.Length > 1000)) throw new InvalidDataException("InvalidModelOutput");
        return value with { Suggestions=value.Suggestions.Select(s=>s with{Title=candidates[s.RecipeId].Title}).ToArray(), Recipes = recipes, Purchases = purchases };
    }

    public static AiRecipe ValidateRecipe(AiRecipe recipe, AiContext context, HashSet<int>? excluded = null, bool extracting = false)
    {
        if (string.IsNullOrWhiteSpace(recipe.Title) || recipe.Title.Length > 150 || recipe.Description?.Length > 12000 ||
            string.IsNullOrWhiteSpace(recipe.Instructions) || recipe.Instructions.Length > 60000 || recipe.Ingredients is null || recipe.Ingredients.Length is < 1 or > 300 ||
            recipe.Servings is <= 0 or > 1000 || recipe.PrepMinutes is < 0 or > 10080 || recipe.CookMinutes is < 0 or > 10080)
            throw new InvalidDataException("InvalidModelOutput");
        var lines = recipe.Ingredients.Select(line => {
            if (string.IsNullOrWhiteSpace(line.Name) || line.Name.Length > 150 || line.Original.Length > 1000 || line.Notes?.Length > 255) throw new InvalidDataException("InvalidModelOutput");
            var ingredient = context.Ingredients.FirstOrDefault(i => string.Equals(i.Name, line.Name, StringComparison.OrdinalIgnoreCase));
            if (!extracting && ingredient is not null && excluded?.Contains(ingredient.IngredientID) == true) throw new InvalidDataException("ExcludedIngredient");
            return line with { IngredientId = ingredient?.IngredientID,
                UnitId = context.Units.FirstOrDefault(u => string.Equals(u.Abbreviation, line.Unit, StringComparison.OrdinalIgnoreCase) || u.UnitID == line.UnitId)?.UnitID,
                Quantity = line.Quantity is {} q && Quantity(q) ? q : null };
        }).ToArray();
        return recipe with { Ingredients = lines, CategoryId = context.Categories.Any(c => c.CategoryID == recipe.CategoryId) ? recipe.CategoryId : null };
    }
}
