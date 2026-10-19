namespace MealPrepApp.Models;

/// <summary>A draft as shown in the "Drafts" list — just enough to pick one.</summary>
public sealed class RecipeDraftListItem
{
    public int DraftID { get; set; }
    public string? Title { get; set; }
    public int? CategoryID { get; set; }
    public string? CategoryName { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A draft loaded in full to repopulate the editor. Every content field is optional —
/// a draft may be incomplete. <see cref="IngredientsJson"/> is the serialized
/// <see cref="DraftIngredientInput"/> array (the DB stores it as an opaque blob).</summary>
public sealed class RecipeDraftFull
{
    public int DraftID { get; set; }
    public int UserID { get; set; }
    public int? CategoryID { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? Servings { get; set; }
    public string? IngredientsJson { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>An ingredient line inside a draft. Unlike <see cref="RecipeIngredientInput"/>, the
/// ingredient and unit are nullable so a half-filled editor row can be saved and restored.</summary>
public sealed class DraftIngredientInput
{
    public int? IngredientID { get; set; }
    public int? UnitID { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}
