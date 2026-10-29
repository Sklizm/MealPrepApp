using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Lookup lists: wraps sp_GetUnits, sp_GetCategories, sp_GetIngredientCategories.
/// These rarely change, so callers may cache the results for the session.</summary>
public class LookupRepository : RepositoryBase
{
    public LookupRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<Unit>> GetUnitsAsync(CancellationToken cancellationToken = default) =>
        QueryProcAsync<Unit>("sp_GetUnits", cancellationToken: cancellationToken);

    public virtual Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        QueryProcAsync<Category>("sp_GetCategories", cancellationToken: cancellationToken);

    public virtual Task<IReadOnlyList<IngredientCategory>> GetIngredientCategoriesAsync(CancellationToken cancellationToken = default) =>
        QueryProcAsync<IngredientCategory>("sp_GetIngredientCategories", cancellationToken: cancellationToken);
}
