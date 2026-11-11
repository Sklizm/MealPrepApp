using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
namespace MealPrepApp.Api;
public sealed class DeviceSession
{
 public Guid SessionID{get;set;}public int UserID{get;set;}public string Username{get;set;}="";public string DeviceName{get;set;}="";
 public DateTime CreatedAt{get;set;}public DateTime ExpiresAt{get;set;}public DateTime? RevokedAt{get;set;}
}
public sealed class CompanionRepository(IDbConnectionFactory factory):InventoryRepository(factory)
{
 public Task<DeviceSession?> ResolveAsync(byte[] tokenHash)=>QuerySingleOrDefaultProcAsync<DeviceSession>("sp_ResolveDeviceSession",new{TokenHash=tokenHash});
 public Task<DeviceSession> CreateSessionAsync(int userId,Guid sessionId,byte[] tokenHash,string expectedHash,string deviceName)=>QuerySingleProcAsync<DeviceSession>("sp_CreateDeviceSession",new{UserID=userId,SessionID=sessionId,TokenHash=tokenHash,ExpectedPasswordHash=expectedHash,DeviceName=deviceName});
 public Task<IReadOnlyList<DeviceSession>> SessionsAsync(int userId)=>QueryProcAsync<DeviceSession>("sp_GetDeviceSessions",new{UserID=userId});
 public Task<string?> OperationAsync(int userId,Guid operation)=>ExecuteScalarProcAsync<string?>("sp_GetClientOperationResult",new{UserID=userId,OperationID=operation});
 public Task<MutationResult> RevokeAsync(int userId,Guid operation,Guid session)=>Mutation("sp_RevokeDeviceSession",new{UserID=userId,OperationID=operation,SessionID=session});
 public Task<MutationResult> CreateListAsync(int userId,CreateListRequest input)=>Mutation("sp_CreateCompanionList",new{UserID=userId,OperationID=input.OperationId,StartDate=input.StartDate.ToDateTime(TimeOnly.MinValue),EndDate=input.EndDate.ToDateTime(TimeOnly.MinValue)});
 internal static DateTimeOffset Utc(DateTime date)=>new(DateTime.SpecifyKind(date,DateTimeKind.Utc));
 private static DateOnly? Day(DateTime? date)=>date.HasValue?DateOnly.FromDateTime(date.Value):null;
 public Task<SyncResponse> SnapshotAsync(int userId,CancellationToken token=default)=>QueryMultipleProcAsync<SyncResponse>("sp_GetCompanionSnapshot",new{UserID=userId},async grid=>{
  var lists=(await grid.ReadAsync<ShoppingSnapshot>()).ToArray();var items=(await grid.ReadAsync<ShoppingListRow>()).ToArray();
  var batches=(await grid.ReadAsync<InventoryBatch>()).ToArray();var thresholds=(await grid.ReadAsync<StockThreshold>()).ToArray();
  var ingredients=(await grid.ReadAsync<Ingredient>()).ToArray();var units=(await grid.ReadAsync<Unit>()).ToArray();var maps=(await grid.ReadAsync<UnitMap>()).ToArray();
  try {return new(1,DateTimeOffset.UtcNow,lists.Select(s=>new SnapshotDto(s.SnapshotID,DateOnly.FromDateTime(s.StartDate),DateOnly.FromDateTime(s.EndDate),Utc(s.CreatedAt))).ToArray(),
   items.Select(i=>new ShoppingDto(i.ItemID,i.SnapshotID,i.Version,i.IngredientID,i.UnitID,i.IngredientName,i.CategoryName,i.UnitAbbreviation,i.NeededQty,i.OnHandQty,i.ToBuyQty,i.IsPurchased,i.PurchasedQuantity,i.PurchasedUnitID,i.PurchasedUnitAbbreviation,Day(i.PurchasedOn),Day(i.ExpiresOn),i.IsManual,i.IsImported,i.IsDeleted)).ToArray(),
   batches.Select(b=>new BatchDto(b.BatchID,b.IngredientID,b.UnitID,b.IngredientName,b.CategoryName,b.UnitAbbreviation,b.RemainingQuantity,Day(b.PurchasedOn),Day(b.ExpiresOn),b.Version,Utc(b.UpdatedAt))).ToArray(),
   thresholds.Select(t=>new ThresholdDto(t.IngredientID,t.UnitID,t.Quantity,t.Version)).ToArray(),ingredients.Select(i=>new IngredientDto(i.IngredientID,i.Name,i.DefaultUnitID,i.IngredientCategoryName)).ToArray(),
   units.Select(u=>new UnitDto(u.UnitID,u.Name,u.Abbreviation,u.UnitType)).ToArray(),maps.Select(m=>new UnitMapDto(m.UnitID,m.CanonicalUnitID,m.Multiplier)).ToArray());
  }finally{foreach(var item in lists.Cast<IDisposable>().Concat(items).Concat(batches))item.Dispose();}
 },token);
}
