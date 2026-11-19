using System.Security.Cryptography;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using SkiaSharp;

namespace MealPrepApp.Api.Ai;

public sealed class AiService(AiOptions options, AiRepository data, RecipeRepository recipes,
    IngredientRepository ingredients, LookupRepository lookups, InventoryRepository inventory,
    MealPlanRepository planning, CookingBatchRepository cooking, NutritionRepository nutrition)
{
    public const string NoticeVersion = "gemini-free-1";
    private DateTimeOffset? _retryAfter;
    public AiCapabilities Capabilities => new(options.Available, !string.IsNullOrWhiteSpace(options.ApiKey), "Gemini", options.Model,
        options.Tasks.Intersect(AiValidation.Tasks).ToArray(), true, NoticeVersion, _retryAfter);
    public void Quota(DateTimeOffset? after) => _retryAfter = after;
    public void RequireAvailable(string kind)
    {
        if (!options.Available || !options.Tasks.Contains(kind)) throw new AiProviderException("Disabled");
        if (_retryAfter > DateTimeOffset.UtcNow) throw new AiProviderException("QuotaExceeded", _retryAfter);
    }
    public async Task<AiContext> ContextAsync(int userId, DateOnly date, decimal servings, CancellationToken token)
    {
        var stock = await inventory.GetBatchesAsync(userId, token: token);
        var maps = await inventory.GetUnitMapAsync(token);
        var catalog = await ingredients.GetIngredientsAsync(cancellationToken: token);
        var units = await lookups.GetUnitsAsync(token);
        var categories = await lookups.GetCategoriesAsync(token);
        var plan = await planning.GetWeeklyPlanAsync(userId, date.ToDateTime(TimeOnly.MinValue), token);
        var batches = await cooking.GetCookingBatchesAsync(userId, token);
        var rows = await recipes.BrowseReadyAsync(userId, date.ToDateTime(TimeOnly.MinValue), servings, "", null, 1, 30, token);
        var candidates = new List<AiCandidate>();
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();
            if (await recipes.GetRecipeFullAsync(row.RecipeID) is not { } recipe) continue;
            candidates.Add(new(recipe.RecipeID, recipe.Title, recipe.Instructions, recipe.Servings, recipe.PrepTimeMinutes,
                recipe.CookTimeMinutes, recipe.CategoryID, recipe.RowVersion, recipe.Ingredients.ToArray(),
                InventoryCalculator.Readiness(recipe, servings, stock, maps, date), await nutrition.GetRecipeNutritionAsync(recipe.RecipeID)));
        }
        return new(await data.DatabaseIdAsync(), catalog.ToArray(), units.ToArray(), maps.ToArray(), categories.ToArray(),
            candidates.ToArray(), stock.ToArray(), plan.ToArray(), batches.ToArray());
    }
    public async Task<AiJob?> SubmitAsync(int userId, Guid sessionId, AiRequest request, CancellationToken token)
    {
        AiValidation.Request(request); RequireAvailable(request.Kind);
        var context = await ContextAsync(userId, request.StartDate, request.Preferences.Servings, token);
        if ((request.ReplaceEntryIds ?? []).Any(id => !context.Plan.Any(p => p.MealPlanEntryID == id && !p.IsPrepared && p.ConsumedPortions == 0)))
            throw new InvalidDataException("InvalidReplacement");
        if (request.UploadId is {} upload && await data.UploadAsync(userId, upload) is null) throw new InvalidDataException("ImageUnavailable");
        string? extracted = null;
        string[]? photoCandidates = null;
        if (request.Kind == "import" && Uri.TryCreate(request.Source?.Trim(), UriKind.Absolute, out var uri))
        {
            var (pageUri, html) = await RecipePageFetcher.FetchDocumentAsync(uri, token);
            try
            {
                var parsed = RecipeImportParser.ParseStructuredPage(html, context.Ingredients, context.Units);
                photoCandidates = RecipeLinkImporter.GetPhotoCandidates(pageUri, html, parsed);
                // Preserve source URL; use extracted structured content rather than arbitrary page markup.
                extracted = AiJson.Write(parsed);
            }
            catch (InvalidDataException)
            {
                photoCandidates = RecipeLinkImporter.GetPhotoCandidates(pageUri, html, null);
                // Unsupported pages retain visible text only, within the same source limit.
                var text = System.Text.RegularExpressions.Regex.Replace(html, @"<(script|style)\b[^>]*>.*?</\1>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline, TimeSpan.FromMilliseconds(250));
                text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(250));
                text = System.Net.WebUtility.HtmlDecode(text); extracted = text[..Math.Min(180000, text.Length)];
            }
        }
        return await data.JobAsync("create", userId, request.OperationId, sessionId, request.Kind, AiJson.Write(new AiWork(request, context, extracted, photoCandidates)));
    }
    public static byte[] NormalizeImage(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0 || (long)codec.Info.Width * codec.Info.Height > 24000000)
            throw new InvalidDataException("InvalidImage");
        using var bitmap = SKBitmap.Decode(bytes) ?? throw new InvalidDataException("InvalidImage");
        var factor = Math.Min(1d, 2400d / Math.Max(bitmap.Width, bitmap.Height));
        using var resized = bitmap.Resize(new SKImageInfo(Math.Max(1, (int)(bitmap.Width * factor)), Math.Max(1, (int)(bitmap.Height * factor))), SKFilterQuality.Medium)
            ?? throw new InvalidDataException("InvalidImage");
        using var encoded = resized.Encode(SKEncodedImageFormat.Jpeg, 88);
        return encoded.ToArray(); // Re-encoding removes EXIF and embedded source data.
    }
    public string ImagePath(Guid id) => Path.Combine(options.UploadDirectory, id.ToString("N") + ".jpg");
    public async Task<Guid> UploadAsync(int userId, byte[] bytes, CancellationToken token)
    {
        RequireAvailable(options.Tasks.Contains("recipe-photo")?"recipe-photo":"receipt");
        var normalized = NormalizeImage(bytes); var id = Guid.NewGuid();
        Directory.CreateDirectory(options.UploadDirectory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(options.UploadDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = ImagePath(id);
        try {
            await File.WriteAllBytesAsync(path, normalized, token);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await data.UploadAsync(userId, id, SHA256.HashData(normalized)); return id;
        } catch { File.Delete(path); throw; }
    }
    public void CleanupImages()
    {
        if (!Directory.Exists(options.UploadDirectory)) return;
        foreach (var file in Directory.EnumerateFiles(options.UploadDirectory, "*.jpg"))
            if (File.GetCreationTimeUtc(file) < DateTime.UtcNow.AddHours(-24)) File.Delete(file);
    }
}

public sealed class AiWorker(AiOptions options, AiService service, AiRepository data, IAiProvider provider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var recovered = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            AiJob? job = null; string? imagePath = null;
            try
            {
                // Retention continues while cloud inference is disabled or its key is removed.
                if (!recovered) { await data.JobAsync("recover"); recovered = true; }
                await data.JobAsync("cleanup"); service.CleanupImages();
                if (options.Available)
                {
                    if (service.Capabilities.RetryAfter is not {} retry || retry <= DateTimeOffset.UtcNow)
                        job = await data.JobAsync("claim");
                    if (job is not null)
                    {
                        var work = AiJson.Read<AiWork>(job.WorkJson!); service.RequireAvailable(work.Request.Kind);
                        byte[]? image = null;
                        if (work.Request.UploadId is {} upload)
                        {
                            if (await data.UploadAsync(job.UserId, upload) is null) throw new AiProviderException("ImageUnavailable");
                            imagePath = service.ImagePath(upload); image = await File.ReadAllBytesAsync(imagePath, stoppingToken);
                        }
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken); timeout.CancelAfter(TimeSpan.FromSeconds(90));
                        // Cancellation is also observed while provider inference is in flight.
                        var inference = provider.GenerateAsync(work, image, timeout.Token);
                        while (!inference.IsCompleted)
                        {
                            await Task.WhenAny(inference, Task.Delay(1000, timeout.Token));
                            timeout.Token.ThrowIfCancellationRequested();
                            await data.JobAsync("cleanup");
                            var current = await data.JobAsync("get", job.UserId, job.JobId);
                            var consent = await data.PreferencesAsync(job.UserId);
                            if (current?.State != "Running" || consent.AcceptedAt is null || consent.NoticeVersion!=AiService.NoticeVersion) { timeout.Cancel(); break; }
                        }
                        var response = await inference;
                        var validated = AiValidation.Proposal(response.Proposal, work);
                        var photo = work.Request.Kind == "import" && work.PhotoCandidates is { Length: > 0 }
                            ? await RecipeLinkImporter.FetchPhotoAsync(work.PhotoCandidates, stoppingToken)
                            : null;
                        validated = validated with
                        {
                            Recipes = validated.Recipes.Select(recipe => recipe with
                            {
                                Source = work.Request.Kind switch
                                {
                                    "generate" => "AI-generated draft",
                                    "recipe-photo" => "Reviewed photo extraction",
                                    _ => work.Request.Source
                                },
                                Photo = photo
                            }).ToArray()
                        };
                        await data.JobAsync("complete", job.UserId, job.JobId, result: AiJson.Write(validated), input: response.InputTokens, output: response.OutputTokens);
                    }
                }
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                var code = error is AiProviderException providerError ? providerError.Code : error is OperationCanceledException ? "TimedOut" : error is InvalidDataException or System.Text.Json.JsonException ? "InvalidModelOutput" : "Unavailable";
                if (error is AiProviderException { Code: "QuotaExceeded" } quota) service.Quota(quota.RetryAfter ?? DateTimeOffset.UtcNow.AddMinutes(1));
                if (job is not null) { try { await data.JobAsync("fail", job.UserId, job.JobId, error: code); } catch { /* Storage failure is retried through startup recovery. */ } }
            }
            finally { if (imagePath is not null) { try { File.Delete(imagePath); } catch (IOException) { } } }
            await Task.Delay(2000, stoppingToken);
        }
    }
}
