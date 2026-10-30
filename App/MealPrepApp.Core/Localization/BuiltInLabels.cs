namespace MealPrepApp.Services;

/// <summary>Translate known catalog identities at the presentation boundary. Never rewrite stored names.</summary>
public static class BuiltInLabels
{
    private static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Toate"] = "Text.All",
        ["Ingredient"] = "Text.Ingredient165",
        ["Titlu"] = "Text.Title249",
        ["Cantitate"] = "Text.Quantity221",
        ["Adăugat recent"] = "Text.RecentlyAdded",
        ["De cumpărat"] = "Text.ToBuy342",
        ["Cumpărate"] = "Text.Purchased",
        ["Cele mai noi"] = "Choice.Newest",
        ["Timp de preparare"] = "Text.PreparationTime",
        ["Mic dejun"] = "Text.Breakfast",
        ["Prânz"] = "Text.Lunch371",
        ["Cină"] = "Text.Dinner372",
        ["Gustare"] = "Text.Snack",
        ["Desert"] = "Category.Dessert",
        ["Băutură"] = "Text.Drink",
        ["Produse"] = "Category.Produce",
        ["Lactate si oua"] = "Text.DairyAndEggs",
        ["Carne si peste"] = "Text.MeatAndFish",
        ["Conserve"] = "Category.Canned",
        ["Condimente si ierburi"] = "Text.SpicesAndHerbs",
        ["Cereale si paste"] = "Text.GrainsAndPasta",
        ["Bauturi"] = "Category.Drinks",
        ["Altele"] = "Category.Other",
        ["Toate categoriile"] = "Text.AllCategories",
        ["Fara categorie"] = "Text.Uncategorized334",
        ["Fără categorie"] = "Text.Uncategorized",
        ["Pranz"] = "Text.Lunch",
        ["Cina"] = "Text.Dinner",
    };
    public static string Display(string? identity) => identity is null ? "" : Keys.TryGetValue(identity, out var key) ? L.Text(key) : identity;
}
