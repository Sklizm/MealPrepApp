using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Legacy computed lists plus per-user snapshots, purchase state and idempotent imports.
/// All access remains through stored procedures.</summary>
public sealed class ShoppingListRepository : RepositoryBase
{
    public ShoppingListRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<ShoppingListRow>> GetShoppingListAsync(
        int userId, DateTime startDate, DateTime endDate) =>
        QueryProcAsync<ShoppingListRow>("sp_GetShoppingList",
            new { UserID = userId, StartDate = startDate.Date, EndDate = endDate.Date });

    public Task<int> CreateSnapshotAsync(int userId,DateTime start,DateTime end) => ExecuteScalarProcAsync<int>("sp_CreateShoppingSnapshot",new {UserID=userId,StartDate=start.Date,EndDate=end.Date});
    public Task<IReadOnlyList<ShoppingSnapshot>> GetSnapshotsAsync(int userId) => QueryProcAsync<ShoppingSnapshot>("sp_GetShoppingSnapshots",new {UserID=userId});
    public Task<IReadOnlyList<ShoppingListRow>> GetSnapshotItemsAsync(int userId,int snapshotId) => QueryProcAsync<ShoppingListRow>("sp_GetShoppingSnapshotItems",new {UserID=userId,SnapshotID=snapshotId});
    public Task SetPurchasedAsync(int userId,int itemId,bool purchased) => ExecuteProcAsync("sp_SetShoppingPurchased",new {UserID=userId,ItemID=itemId,IsPurchased=purchased});
    public Task ImportItemAsync(int userId,int itemId) => ExecuteProcAsync("sp_ImportShoppingItem",new {UserID=userId,ItemID=itemId});
}
