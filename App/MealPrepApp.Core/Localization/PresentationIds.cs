namespace MealPrepApp.Services;

// Values retain the legacy API/procedure contracts. Display labels are resolved separately.
public static class RecipeSortIds
{
    public const string Title = "Titlu";
    public const string Newest = "Cele mai noi";
    public const string PreparationTime = "Timp de preparare";
}
public static class PantrySortIds
{
    public const string Ingredient = "Ingredient";
    public const string Quantity = "Cantitate";
    public const string RecentlyAdded = "Adăugat recent";
}
public static class PurchaseFilterIds
{
    public const string All = "Toate";
    public const string ToBuy = "De cumpărat";
    public const string Purchased = "Cumpărate";
}
public static class RecipeFilterIds
{
    public const string All = "Toate";
    public const string Favorites = "Favorite";
    public const string Recent = "Recente";
    public const string Drafts = "Drafts";
    public const string Pantry = "Frigider";
}
