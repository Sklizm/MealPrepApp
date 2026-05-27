using System.Text.Json;
using MealPrepApp.Models;

namespace MealPrepApp.Data.Repositories;

/// <summary>Recipe drafts (partially-complete recipes): wraps sp_SaveDraft, sp_GetDrafts,
/// sp_GetDraft, sp_DeleteDraft. Ingredient lines are serialized to JSON; the DB stores the
/// blob opaquely.</summary>
public sealed class DraftRepository : RepositoryBase
{
    public DraftRepository(IDbConnectionFactory factory) : base(factory) { }

    /// <summary>Inserts a new draft (<paramref name="draftId"/> null) or updates an existing one;
    /// returns the DraftID. No validation — a draft may be incomplete.</summary>
    public Task<int> SaveDraftAsync(
        int userId, int? draftId, int? categoryId, string? title, string? description,
        string? instructions, int? prepTimeMinutes, int? cookTimeMinutes, int? servings,
        IReadOnlyList<DraftIngredientInput> ingredients) =>
        ExecuteScalarProcAsync<int>("sp_SaveDraft", new
        {
            UserID = userId,
            DraftID = draftId,
            CategoryID = categoryId,
            Title = title,
            Description = description,
            Instructions = instructions,
            PrepTimeMinutes = prepTimeMinutes,
            CookTimeMinutes = cookTimeMinutes,
            Servings = servings,
            IngredientsJson = JsonSerializer.Serialize(ingredients)
        });

    public Task<IReadOnlyList<RecipeDraftListItem>> GetDraftsAsync(int userId) =>
        QueryProcAsync<RecipeDraftListItem>("sp_GetDrafts", new { UserID = userId });

    public Task<RecipeDraftFull?> GetDraftAsync(int draftId, int userId) =>
        QuerySingleOrDefaultProcAsync<RecipeDraftFull>("sp_GetDraft",
            new { DraftID = draftId, UserID = userId });

    public Task DeleteDraftAsync(int draftId, int userId) =>
        ExecuteProcAsync("sp_DeleteDraft", new { DraftID = draftId, UserID = userId });
}
