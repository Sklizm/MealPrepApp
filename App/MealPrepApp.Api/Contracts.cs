using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MealPrepApp.Models;
namespace MealPrepApp.Api;
public sealed record LoginRequest(Guid OperationId,string Identifier,string Password,string DeviceName);
public sealed record LoginResponse(Guid SessionId,int UserId,string Username,string Token,DateTimeOffset ExpiresAt,int ProtocolVersion=1);
public sealed record OperationRequest(Guid OperationId);
public sealed record VersionedRequest(Guid OperationId,long ExpectedVersion);
public sealed record CreateListRequest(Guid OperationId,DateOnly StartDate,DateOnly EndDate);
public sealed record ShoppingMutation(Guid OperationId,long ExpectedVersion,int SnapshotId,string Name,int? IngredientId,
 int? UnitId,decimal RequiredQuantity,bool IsPurchased,decimal? PurchasedQuantity,int? PurchasedUnitId,DateOnly? PurchasedOn,DateOnly? ExpiresOn,bool IsDeleted=false)
{
 public ShoppingChange ToChange(int? itemId)=>new(SnapshotId,itemId,ExpectedVersion,Name,IngredientId,UnitId,RequiredQuantity,IsPurchased,PurchasedQuantity,PurchasedUnitId,PurchasedOn?.ToDateTime(TimeOnly.MinValue),ExpiresOn?.ToDateTime(TimeOnly.MinValue),IsDeleted);
}
public sealed record StockAdjustment(Guid OperationId,long ExpectedVersion,decimal Delta,string Kind="Correction");
public sealed record AddStock(Guid OperationId,int IngredientId,int UnitId,decimal Quantity,DateOnly? PurchasedOn,DateOnly? ExpiresOn);
public sealed record SetThreshold(Guid OperationId,long ExpectedVersion,int UnitId,decimal Quantity);
public sealed record SnapshotDto(int SnapshotId,DateOnly StartDate,DateOnly EndDate,DateTimeOffset CreatedAt);
public sealed record ShoppingDto(int ItemId,int SnapshotId,long Version,int? IngredientId,int? UnitId,string Name,string CategoryName,string UnitAbbreviation,
 decimal NeededQuantity,decimal OnHandQuantity,decimal RequiredQuantity,bool IsPurchased,decimal? PurchasedQuantity,int? PurchasedUnitId,string PurchasedUnitAbbreviation,
 DateOnly? PurchasedOn,DateOnly? ExpiresOn,bool IsManual,bool IsImported,bool IsDeleted);
public sealed record BatchDto(int BatchId,int IngredientId,int UnitId,string IngredientName,string CategoryName,string UnitAbbreviation,
 decimal RemainingQuantity,DateOnly? PurchasedOn,DateOnly? ExpiresOn,long Version,DateTimeOffset UpdatedAt);
public sealed record ThresholdDto(int IngredientId,int UnitId,decimal Quantity,long Version);
public sealed record IngredientDto(int IngredientId,string Name,int? DefaultUnitId,string? CategoryName);
public sealed record UnitDto(int UnitId,string Name,string Abbreviation,string UnitType);
public sealed record UnitMapDto(int UnitId,int CanonicalUnitId,decimal Multiplier);
public sealed record SyncResponse(int ProtocolVersion,DateTimeOffset ServerUtcNow,IReadOnlyList<SnapshotDto> Lists,IReadOnlyList<ShoppingDto> Items,
 IReadOnlyList<BatchDto> Batches,IReadOnlyList<ThresholdDto> Thresholds,IReadOnlyList<IngredientDto> Ingredients,IReadOnlyList<UnitDto> Units,IReadOnlyList<UnitMapDto> UnitMap);
public sealed class DecimalStringConverter : JsonConverter<decimal>
{
 public override decimal Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options) {
  if(reader.TokenType==JsonTokenType.Number&&reader.TryGetDecimal(out var n))return n;
  if(reader.TokenType==JsonTokenType.String&&decimal.TryParse(reader.GetString(),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value))return value;
  throw new JsonException("A decimal quantity is required.");
 }
 public override void Write(Utf8JsonWriter writer,decimal value,JsonSerializerOptions options)=>writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
