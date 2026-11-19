using MealPrepApp.Models;

namespace MealPrepApp.Services;

public static class RecipeLinkImporter
{
    public static async Task<(ImportedRecipe Recipe, ProcessedRecipeImage? Photo)> ReviewAsync(
        Uri uri, IReadOnlyList<Ingredient> ingredients, IReadOnlyList<Unit> units, CancellationToken token = default)
    {
        var (pageUri, html) = await RecipePageFetcher.FetchDocumentAsync(uri, token);
        var recipe = RecipeImportParser.ParseStructuredPage(html, ingredients, units);
        var photo = await FetchPhotoAsync(GetPhotoCandidates(pageUri, html, recipe), token);
        return (recipe, photo);
    }

    public static string[] GetPhotoCandidates(Uri pageUri, string html, ImportedRecipe? recipe)
    {
        try
        {
            return (recipe?.ImageUrls ?? []).Concat(RecipeImportParser.OpenGraphImageUrls(html))
                .Select(url => Uri.TryCreate(pageUri, url.Trim(), out var resolved) ? resolved : null)
                .OfType<Uri>()
                .Where(uri => uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 && uri.UserInfo.Length == 0)
                .Select(uri => uri.AbsoluteUri)
                .Distinct(StringComparer.Ordinal)
                .Take(4)
                .ToArray();
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return [];
        }
    }

    public static async Task<ProcessedRecipeImage?> FetchPhotoAsync(IEnumerable<string> candidates, CancellationToken token)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(6));
        foreach (var url in candidates.Take(4))
        {
            if (budget.IsCancellationRequested) break;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;

            try
            {
                var bytes = await RecipePageFetcher.FetchImageAsync(uri, budget.Token);
                return RecipeImageProcessor.ProcessImportedPhoto(bytes);
            }
            catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException or InvalidOperationException ||
                                          error is OperationCanceledException && !token.IsCancellationRequested)
            {
                // A missing, inaccessible, or invalid photo leaves the text import available.
            }
        }
        token.ThrowIfCancellationRequested();
        return null;
    }
}
