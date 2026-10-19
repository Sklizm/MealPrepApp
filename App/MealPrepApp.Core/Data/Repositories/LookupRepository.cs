using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Lookup lists: wraps sp_GetUnits, sp_GetCategories, sp_GetIngredientCategories.
/// These rarely change, so callers may cache the results for the session.</summary>
public sealed class LookupRepository : RepositoryBase
{
    public LookupRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<Unit>> GetUnitsAsync() =>
        QueryProcAsync<Unit>("sp_GetUnits");

    public Task<IReadOnlyList<Category>> GetCategoriesAsync() =>
        QueryProcAsync<Category>("sp_GetCategories");

    public Task<IReadOnlyList<IngredientCategory>> GetIngredientCategoriesAsync() =>
        QueryProcAsync<IngredientCategory>("sp_GetIngredientCategories");
}
