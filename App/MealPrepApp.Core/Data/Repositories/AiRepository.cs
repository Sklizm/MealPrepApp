using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

public sealed record AiPreferenceRow(string PreferencesJson, string? NoticeVersion, DateTime? AcceptedAt, long Version);
public sealed record AiUploadRow(Guid UploadId, byte[] ContentHash, DateTime ExpiresAt);
public class AiRepository(IDbConnectionFactory factory) : InventoryRepository(factory)
{
    public virtual Task<Guid> DatabaseIdAsync() => ExecuteScalarProcAsync<Guid>("sp_GetApplicationIdentity");
    public virtual Task<AiPreferenceRow> PreferencesAsync(int userId, AiPreferences? preferences = null, AiConsent? consent = null) =>
        QuerySingleProcAsync<AiPreferenceRow>("sp_AssistantPreferences", new { UserID = userId, Json = preferences is null ? null : AiJson.Write(preferences), Notice = consent?.NoticeVersion, Accept = consent?.Accepted });
    public virtual Task<AiJob?> JobAsync(string action, int userId = 0, Guid? id = null, Guid? session = null,
        string? kind = null, string? work = null, string? result = null, string? error = null, int input = 0, int output = 0) =>
        QuerySingleOrDefaultProcAsync<AiJob>("sp_AssistantJob", new { Action = action, UserID = userId, JobID = id, SessionID = session,
            Kind = kind, Work = work, Result = result, Error = error, InputTokens = input, OutputTokens = output });
    public virtual Task<IReadOnlyList<AiJob>> JobsAsync(int userId) => QueryProcAsync<AiJob>("sp_AssistantJob", new { Action = "list", UserID = userId });
    public virtual Task<AiUsage> UsageAsync(int userId) => QuerySingleProcAsync<AiUsage>("sp_AssistantJob", new { Action = "usage", UserID = userId });
    public virtual Task<IReadOnlyList<int>> ReceiptRowsAsync(int userId,Guid jobId)=>QueryProcAsync<int>("sp_GetAcceptedReceiptRows",new{UserID=userId,JobID=jobId});
    public virtual Task<AiUploadRow?> UploadAsync(int userId, Guid id, byte[]? hash = null) =>
        QuerySingleOrDefaultProcAsync<AiUploadRow>("sp_AssistantUpload", new { UserID = userId, UploadID = id, Hash = hash });
    public virtual Task<MutationResult> ApplyPlanAsync(int userId, AiPlanApply value) => Mutation("sp_ApplyReviewedPlan", new {
        UserID = userId, OperationID = value.OperationId, Meals = AiJson.Write(value.Meals), Replace = AiJson.Write(value.ReplaceEntryIds),
        Recipes = AiJson.Write(value.RecipeVersions), Entries = AiJson.Write(value.EntryVersions), Batches = AiJson.Write(value.BatchVersions) });
    public virtual Task<MutationResult> AcceptReceiptAsync(int userId, Guid jobId, AiReceiptApply value) => Mutation("sp_AcceptReceipt", new {
        UserID = userId, OperationID = value.OperationId, JobID = jobId, value.ExpectedVersion, Indexes = AiJson.Write(value.RowIndexes),
        Purchases = AiJson.Write(value.Purchases), value.SnapshotId, value.ConfirmDuplicate });
}
