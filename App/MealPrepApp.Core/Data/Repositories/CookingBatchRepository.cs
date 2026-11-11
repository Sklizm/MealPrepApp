using System.Text.Json;
using MealPrepApp.Models;
namespace MealPrepApp.Data.Repositories;
public class CookingBatchRepository(IDbConnectionFactory factory) : InventoryRepository(factory)
{
    public virtual Task<IReadOnlyList<CookingBatch>> GetCookingBatchesAsync(int userId, CancellationToken token = default) =>
        QueryProcAsync<CookingBatch>("sp_GetCookingBatches", new { UserID = userId }, token);
    public virtual Task<MutationResult> PlanAsync(int userId, Guid operation, int recipeId, decimal portions, IReadOnlyList<MealAllocation> allocations) =>
        Mutation("sp_PlanCookingBatch", new { UserID = userId, OperationID = operation, RecipeID = recipeId,
            PlannedPortions = portions, AllocationsJson = JsonSerializer.Serialize(allocations) });
    public virtual Task<MutationResult> AllocatePreparedAsync(int userId,Guid operation,CookingBatch batch,IReadOnlyList<MealAllocation> allocations)=>
        Mutation("sp_AllocatePreparedMeals",new {UserID=userId,OperationID=operation,batch.CookingBatchID,ExpectedVersion=batch.Version,AllocationsJson=JsonSerializer.Serialize(allocations)});
    public virtual Task<MutationResult> PlanSeparateAsync(int userId,Guid operation,int recipeId,IReadOnlyList<MealAllocation> allocations) =>
        Mutation("sp_PlanSeparateMeals",new {UserID=userId,OperationID=operation,RecipeID=recipeId,AllocationsJson=JsonSerializer.Serialize(allocations)});
    public virtual Task<MutationResult> AllocateAsync(int userId, Guid operation, int batchId, long version, MealAllocation allocation) =>
        Mutation("sp_AllocateCookingBatch", new { UserID = userId, OperationID = operation, CookingBatchID = batchId,
            ExpectedVersion = version, allocation.PlannedDate, allocation.CategoryID, allocation.Portions });
    public virtual Task<MutationResult> FinishAsync(int userId, Guid operation, CookingCompletion review) =>
        Mutation("sp_FinishCookingBatch", new { UserID = userId, OperationID = operation, review.CookingBatchID,
            review.ExpectedVersion, review.ProducedPortions, review.ConsumedPortions, review.PreparedOn, review.ExpiresOn,
            DeductionsJson = JsonSerializer.Serialize(review.Deductions), review.IncludeExpired });
    public virtual Task<MutationResult> PortionEventAsync(int userId, Guid operation, int batchId, long version, decimal portions,
        string kind, int? mealPlanEntryId = null) => Mutation("sp_RecordPortionEvent", new { UserID = userId,
            OperationID = operation, CookingBatchID = batchId, ExpectedVersion = version, Portions = portions,
            Kind = kind, MealPlanEntryID = mealPlanEntryId });
    public virtual Task<ActivityReport> GetActivityAsync(int userId, DateTime start, DateTime end) =>
        QueryMultipleProcAsync("sp_GetActivityReport", new { UserID = userId, StartDate = start.Date, EndDate = end.Date,
            UtcStart = start.Date.ToUniversalTime(), UtcEnd = end.Date.AddDays(1).ToUniversalTime() }, async grid =>
            new ActivityReport(await grid.ReadSingleAsync<ActivitySummary>(),
                (await grid.ReadAsync<WasteQuantity>()).ToArray(), (await grid.ReadAsync<MealRepetition>()).ToArray()));
}
