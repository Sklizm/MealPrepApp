namespace MealPrepApp.Models;
public sealed record RecipeBundleInput(int? RecipeID, int? CategoryID, string Title, string? Description,
    string Instructions, int? PrepTimeMinutes, int? CookTimeMinutes, int? Servings,
    IReadOnlyList<RecipeIngredientInput> Ingredients, byte[]? ExpectedVersion, string PhotoMode,
    byte[]? ImageData, byte[]? ThumbnailData);
public sealed record RecipeBundleResult(int Id, byte[] Version);
public sealed record EditorRecovery(int? RecipeID, int? DraftID, int? CategoryID, string Title,
    string? Description, string Instructions, int? PrepTimeMinutes, int? CookTimeMinutes,
    int? Servings, DraftIngredientInput[] Rows, byte[] RowVersion, string PhotoMode,
    byte[]? Photo, byte[]? Thumbnail, string SourceText, Guid SaveOperation, string? SaveFingerprint = null);
