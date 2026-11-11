using System.Text.Json;
using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

public class InventoryRepository(IDbConnectionFactory factory) : RepositoryBase(factory)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public virtual Task<IReadOnlyList<InventoryBatch>> GetBatchesAsync(int userId, bool includeEmpty = false, CancellationToken token = default) =>
        QueryProcAsync<InventoryBatch>("sp_GetInventoryBatches", new { UserID = userId, IncludeEmpty = includeEmpty }, token);
    public virtual Task<IReadOnlyList<UnitMap>> GetUnitMapAsync(CancellationToken token = default) =>
        QueryProcAsync<UnitMap>("sp_GetUnitMap", cancellationToken: token);
    public virtual Task<MutationResult> AddAsync(int userId, Guid operationId, int ingredientId, int unitId, decimal quantity,
        DateTime? purchasedOn = null, DateTime? expiresOn = null) => Mutation("sp_AddInventoryBatch",
            new { UserID = userId, OperationID = operationId, IngredientID = ingredientId, UnitID = unitId,
                Quantity = quantity, PurchasedOn = purchasedOn?.Date, ExpiresOn = expiresOn?.Date });
    public virtual Task<MutationResult> AdjustAsync(int userId, Guid operationId, int batchId, long expectedVersion,
        decimal delta, string kind = "Correction") => Mutation("sp_AdjustInventoryBatch",
            new { UserID = userId, OperationID = operationId, BatchID = batchId, ExpectedVersion = expectedVersion, Delta = delta, Kind = kind });
    public virtual Task<MutationResult> UndoAsync(int userId, Guid operationId, int eventId) => Mutation("sp_UndoStockEvent",
        new { UserID = userId, OperationID = operationId, EventID = eventId });
    protected async Task<MutationResult> Mutation(string procedure, object parameters) =>
        JsonSerializer.Deserialize<MutationResult>(await ExecuteScalarProcAsync<string>(procedure, parameters), Json)
        ?? throw new InvalidDataException("Missing mutation result.");
}
