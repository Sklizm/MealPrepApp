using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Shopping list: wraps sp_GetShoppingList. The list is computed on the fly
/// (planned-meal demand minus pantry stock), never stored.</summary>
public sealed class ShoppingListRepository : RepositoryBase
{
    public ShoppingListRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<ShoppingListRow>> GetShoppingListAsync(
        int userId, DateTime startDate, DateTime endDate) =>
        QueryProcAsync<ShoppingListRow>("sp_GetShoppingList",
            new { UserID = userId, StartDate = startDate.Date, EndDate = endDate.Date });
}
