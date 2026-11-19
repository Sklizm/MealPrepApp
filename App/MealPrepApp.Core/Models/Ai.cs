using System.Text.Json;
using System.Text.Json.Serialization;
using MealPrepApp.Services;

namespace MealPrepApp.Models;

public static class AiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { MaxDepth = 32, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new InvalidDataException("Invalid assistant response.");
}

public sealed record AiPreferences(decimal Servings = 2, int MaxMinutes = 45, int[]? ExcludedIngredientIds = null,
    int[]? MealSlotIds = null, bool PreferExpiring = true, bool UseLeftovers = true, int MaxRepetitions = 2);
public sealed record AiRequest(Guid OperationId, string Kind, string Language, string Prompt, DateOnly StartDate,
    AiPreferences Preferences, string? Source = null, Guid? UploadId = null, bool AllowNewRecipes = false,
    int[]? ReplaceEntryIds = null, AiPlanMeal[]? PinnedMeals = null);
public sealed record AiIngredient(string Original, string Name, decimal? Quantity, int? IngredientId, int? UnitId,
    string? Unit = null, string? Notes = null);
public sealed record AiRecipe(string Title, string Description, string Instructions, int? Servings,
    int? PrepMinutes, int? CookMinutes, int? CategoryId, AiIngredient[] Ingredients, string? Source = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProcessedRecipeImage? Photo = null);
public sealed record AiSuggestion(int RecipeId, string Reason, string? Title = null);
public sealed record AiPlanMeal(DateOnly Date, int SlotId, int? RecipeId, int? DraftIndex, decimal Portions,
    string BatchKey, string Reason, int? PreparedBatchId = null);
public sealed record AiPurchase(string Original, string Name, int? IngredientId, decimal? Quantity,
    int? UnitId, string? Unit, DateOnly? PurchasedOn, DateOnly? ExpiresOn);
public sealed record AiProposal(AiSuggestion[] Suggestions, AiRecipe[] Recipes, AiPlanMeal[] Meals,
    AiPurchase[] Purchases, string[] Warnings)
{
    public static AiProposal Empty => new([], [], [], [], []);
}
public sealed record AiCandidate(int RecipeId, string Title, string Instructions, int? Servings, int? PrepMinutes,
    int? CookMinutes, int? CategoryId, byte[] Version, RecipeIngredientLine[] Ingredients,
    RecipeReadiness Readiness, RecipeNutritionSummary? Nutrition);
public sealed record AiContext(Guid DatabaseId, Ingredient[] Ingredients, Unit[] Units, UnitMap[] UnitMap,
    Category[] Categories, AiCandidate[] Candidates, InventoryBatch[] Stock, MealPlanEntry[] Plan, CookingBatch[] Batches);
public sealed record AiWork(AiRequest Request, AiContext Context, string? ExtractedSource = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? PhotoCandidates = null);
public sealed record AiJob(Guid JobId, int UserId, Guid SessionId, string Kind, string State, long Version,
    string? WorkJson, string? ResultJson, string? ErrorCode, DateTime CreatedAt, DateTime ExpiresAt,
    int InputTokens = 0, int OutputTokens = 0);
public sealed record AiCapabilities(bool Enabled, bool Configured, string Provider, string Model,
    string[] Tasks, bool FreeOnly, string DataNoticeVersion, DateTimeOffset? RetryAfter = null);
public sealed record AiUsage(int Requests, long InputTokens, long OutputTokens);
public sealed record AiRecipeSave(Guid OperationId, RecipeBundleInput Recipe, string? Source = null, string SourceKind = "reviewed");
public sealed record AiPlanApply(Guid OperationId, Guid? JobId, AiPlanMeal[] Meals, int[] ReplaceEntryIds,
    Dictionary<int, string> RecipeVersions, Dictionary<int, long> EntryVersions,
    Dictionary<int, long> BatchVersions, Dictionary<int, int>? PublishedDrafts = null);
public sealed record AiReceiptApply(Guid OperationId, long ExpectedVersion, int[] RowIndexes, AiPurchase[] Purchases,
    int? SnapshotId = null, bool ConfirmDuplicate = false);
public sealed record AiConsent(string NoticeVersion, bool Accepted);
