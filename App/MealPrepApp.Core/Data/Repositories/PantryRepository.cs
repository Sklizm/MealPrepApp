using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Pantry ("Frigider"): wraps sp_AddPantryItem, sp_UpdatePantryQuantity,
/// sp_RemovePantryItem, sp_GetPantry.</summary>
public class PantryRepository : RepositoryBase
{
    public PantryRepository(IDbConnectionFactory factory) : base(factory) { }

    /// <summary>Adds stock. If the same ingredient+unit already exists, the quantity is accumulated
    /// (MERGE upsert in the proc). Quantity must be &gt; 0 or the DB throws error 50000.</summary>
    public virtual Task AddPantryItemAsync(int userId, int ingredientId, int unitId, decimal quantity) =>
        ExecuteProcAsync("sp_AddPantryItem",
            new { UserID = userId, IngredientID = ingredientId, UnitID = unitId, Quantity = quantity });

    /// <summary>Sets an absolute quantity for an existing pantry row (not a delta).</summary>
    public virtual Task UpdatePantryQuantityAsync(int userPantryId, int userId, decimal quantity) =>
        ExecuteProcAsync("sp_UpdatePantryQuantity",
            new { UserPantryID = userPantryId, UserID = userId, Quantity = quantity });

    public virtual Task RemovePantryItemAsync(int userPantryId, int userId) =>
        ExecuteProcAsync("sp_RemovePantryItem", new { UserPantryID = userPantryId, UserID = userId });

    public virtual Task<IReadOnlyList<PantryItem>> GetPantryAsync(int userId, CancellationToken cancellationToken = default) =>
        QueryProcAsync<PantryItem>("sp_GetPantry", new { UserID = userId }, cancellationToken);
}
