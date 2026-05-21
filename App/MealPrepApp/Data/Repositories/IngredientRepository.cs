using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Ingredients: wraps sp_AddIngredient, sp_GetIngredients, sp_SearchIngredients,
/// sp_GetIngredientUsage. Ingredients are global (not user-scoped) in v1.</summary>
public sealed class IngredientRepository : RepositoryBase
{
    public IngredientRepository(IDbConnectionFactory factory) : base(factory) { }

    /// <summary>Adds a global ingredient and returns its new id. Duplicate names are rejected by the DB.
    /// <paramref name="ingredientCategoryId"/> null leaves the ingredient ungrouped.</summary>
    public Task<int> AddIngredientAsync(string name, int? defaultUnitId, int? ingredientCategoryId = null) =>
        ExecuteScalarProcAsync<int>("sp_AddIngredient", new
        {
            Name = name,
            DefaultUnitID = defaultUnitId,
            IngredientCategoryID = ingredientCategoryId,
        });

    /// <summary><paramref name="ingredientCategoryId"/> null returns every ingredient.</summary>
    public Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(int? ingredientCategoryId = null) =>
        QueryProcAsync<Ingredient>("sp_GetIngredients", new { IngredientCategoryID = ingredientCategoryId });

    public Task<IReadOnlyList<IngredientSearchResult>> SearchIngredientsAsync(string term) =>
        QueryProcAsync<IngredientSearchResult>("sp_SearchIngredients", new { Term = term });

    /// <summary>Returns usage info, or null for an unknown id (treat null as zero usage).</summary>
    public Task<IngredientUsage?> GetIngredientUsageAsync(int ingredientId) =>
        QuerySingleOrDefaultProcAsync<IngredientUsage>("sp_GetIngredientUsage",
            new { IngredientID = ingredientId });
}
