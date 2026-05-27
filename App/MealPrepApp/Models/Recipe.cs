namespace MealPrepApp.Models;

/// <summary>
/// List row from <c>sp_GetRecipes</c>, <c>sp_SearchRecipesByTitle</c> and <c>sp_GetFavoriteRecipes</c>.
/// Search results omit the time/serving columns (left null); favorites set <see cref="FavoritedAt"/>.
/// </summary>
public sealed class RecipeListItem
{
    public int RecipeID { get; set; }
    public string Title { get; set; } = "";
    public int UserID { get; set; }
    public string AuthorUsername { get; set; } = "";
    public int? CategoryID { get; set; }
    public string? CategoryName { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? Servings { get; set; }
    public byte[]? PhotoData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FavoritedAt { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>An ingredient line of a recipe (second result set of <c>sp_GetRecipeFull</c>).</summary>
public sealed class RecipeIngredientLine
{
    public int RecipeIngredientID { get; set; }
    public int IngredientID { get; set; }
    public string IngredientName { get; set; } = "";
    public int UnitID { get; set; }
    public string UnitName { get; set; } = "";
    public string UnitAbbreviation { get; set; } = "";
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Full recipe from <c>sp_GetRecipeFull</c>: header + ingredient lines + the
/// <see cref="RowVersion"/> optimistic-concurrency token to pass back to <c>sp_UpdateRecipe</c>.</summary>
public sealed class RecipeFull
{
    public int RecipeID { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Instructions { get; set; } = "";
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? Servings { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public int UserID { get; set; }
    public string AuthorUsername { get; set; } = "";
    public int? CategoryID { get; set; }
    public string? CategoryName { get; set; }

    /// <summary>Populated from the second result set; not a column on the first.</summary>
    public List<RecipeIngredientLine> Ingredients { get; set; } = new();
}

/// <summary>A recipe's photo bytes + content type, from <c>sp_GetRecipePhoto</c>.</summary>
public sealed class RecipePhotoData
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "image/jpeg";
}

/// <summary>Result row from <c>sp_FindRecipesByIngredients</c> ("what can I make with…?").</summary>
public sealed class RecipeFindResult
{
    public int RecipeID { get; set; }
    public string Title { get; set; } = "";
    public int UserID { get; set; }
    public string AuthorUsername { get; set; } = "";
    public int? CategoryID { get; set; }
    public string? CategoryName { get; set; }
    public int MatchedIngredients { get; set; }
    public int TotalIngredients { get; set; }
}

/// <summary>Row from <c>sp_GetRecentRecipes</c> — the Acasa "Retete Recente" grid.</summary>
public sealed class RecentRecipe
{
    public int RecipeID { get; set; }
    public string Title { get; set; } = "";
    public int? CategoryID { get; set; }
    public string? CategoryName { get; set; }
    public int? PrepTimeMinutes { get; set; }
    public int? CookTimeMinutes { get; set; }
    public int? Servings { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime LastTouchedAt { get; set; }
}

/// <summary>An ingredient line supplied when creating/updating a recipe.
/// Serialized to the <c>@IngredientsJson</c> parameter — property names must stay
/// PascalCase to match the proc's OPENJSON paths.</summary>
public sealed class RecipeIngredientInput
{
    public int IngredientID { get; set; }
    public int UnitID { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}
