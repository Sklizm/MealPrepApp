using System.Globalization;
using System.Security.Claims;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.Api.Ai;

public sealed record AddReviewedIngredient(Guid OperationId, string Name, int UnitId);
public sealed record ImportPreviewRequest(string Source);
public static class AiEndpoints
{
    private static int User(ClaimsPrincipal principal) => int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
    public static void AddAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new AiOptions(); configuration.GetSection("Ai").Bind(options);
        services.AddSingleton(options); services.AddSingleton<AiRepository>(); services.AddSingleton<RecipeRepository>();
        services.AddSingleton<IngredientRepository>(); services.AddSingleton<LookupRepository>(); services.AddSingleton<MealPlanRepository>();
        services.AddSingleton<CookingBatchRepository>(); services.AddSingleton<NutritionRepository>(); services.AddSingleton<AiService>();
        services.AddSingleton<IAiProvider>(_ => new GeminiProvider(options, new HttpClient(new SocketsHttpHandler {
            AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(100) }));
        services.AddHostedService<AiWorker>();
    }
    public static void MapAssistant(this RouteGroupBuilder api)
    {
        api.MapGet("/ai/capabilities", (AiService service) => Results.Ok(service.Capabilities));
        api.MapGet("/identity", async (AiRepository data) => Results.Ok(new { databaseId = await data.DatabaseIdAsync() }));
        api.MapGet("/ai/preferences", async (ClaimsPrincipal user, AiRepository data) => Results.Ok(await data.PreferencesAsync(User(user))));
        api.MapPut("/ai/preferences", async (AiPreferences preferences, ClaimsPrincipal user, AiRepository data) => {
            AiValidation.Request(new(Guid.NewGuid(), "suggest", "en", "", DateOnly.FromDateTime(DateTime.Today), preferences));
            return Results.Ok(await data.PreferencesAsync(User(user), preferences));
        });
        api.MapPost("/ai/consent", async (AiConsent consent, ClaimsPrincipal user, AiRepository data) =>
            consent.NoticeVersion != AiService.NoticeVersion ? Results.BadRequest() : Results.Ok(await data.PreferencesAsync(User(user), consent: consent)));
        api.MapGet("/ai/usage", async (ClaimsPrincipal user, AiRepository data) => Results.Ok(await data.UsageAsync(User(user))));
        api.MapGet("/ai/context", async (DateOnly date, decimal? servings, ClaimsPrincipal user, AiService service, CancellationToken token) => {
            if (date < new DateOnly(2000,1,1) || date > new DateOnly(2100,1,1) || servings is <=0 or >1000) return Results.BadRequest();
            return Results.Ok(await service.ContextAsync(User(user), date, servings ?? 2, token));
        });
        api.MapGet("/ai/jobs", async (ClaimsPrincipal user, AiRepository data) => Results.Ok(await data.JobsAsync(User(user))));
        api.MapPost("/ai/jobs", async (AiRequest request, ClaimsPrincipal user, AiService service, CancellationToken token) =>
            Results.Ok(await service.SubmitAsync(User(user), Guid.Parse(user.FindFirstValue("session_id")!), request, token)));
        api.MapGet("/ai/jobs/{jobId:guid}", async (Guid jobId, ClaimsPrincipal user, AiRepository data) =>
            await data.JobAsync("get", User(user), jobId) is {} job ? Results.Ok(job) : Results.NotFound());
        api.MapPost("/ai/jobs/{jobId:guid}/cancel", async (Guid jobId, ClaimsPrincipal user, AiRepository data) => Results.Ok(await data.JobAsync("cancel", User(user), jobId)));
        api.MapPost("/ai/uploads", async (HttpRequest request, ClaimsPrincipal user, AiService service, AiRepository data, CancellationToken token) => {
            var preferences = await data.PreferencesAsync(User(user));
            if (preferences.NoticeVersion != AiService.NoticeVersion || preferences.AcceptedAt is null) return Results.Forbid();
            if (request.Headers["X-Image-Reviewed"] != "true" || request.ContentLength is <=0 or >8388608) return Results.BadRequest();
            using var buffer = new MemoryStream(); var bytes = new byte[8192]; int length;
            while ((length = await request.Body.ReadAsync(bytes, token)) > 0) { if (buffer.Length + length > 8388608) return Results.BadRequest(); await buffer.WriteAsync(bytes.AsMemory(0,length), token); }
            return Results.Ok(new { uploadId = await service.UploadAsync(User(user), buffer.ToArray(), token) });
        });
        api.MapGet("/recipes", async (string? search, int? page, ClaimsPrincipal user, RecipeRepository recipes, CancellationToken token) =>
            Results.Ok(await recipes.BrowseAsync(User(user), "Toate", (search ?? "")[..Math.Min(search?.Length ?? 0,150)], null, Math.Max(page ?? 1,1), 30, token)));
        api.MapGet("/recipes/{id:int}", async (int id, RecipeRepository recipes) => await recipes.GetRecipeFullAsync(id) is {} recipe ? Results.Ok(recipe) : Results.NotFound());
        api.MapGet("/recipes/{id:int}/nutrition", async (int id, NutritionRepository nutrition) => Results.Ok(await nutrition.GetRecipeNutritionAsync(id)));
        api.MapPost("/recipes/import-preview",async(ImportPreviewRequest input,IngredientRepository ingredients,LookupRepository lookups,CancellationToken token)=>{
            if(string.IsNullOrWhiteSpace(input.Source)||input.Source.Length>200000)return Results.BadRequest();
            var catalog=await ingredients.GetIngredientsAsync(cancellationToken:token);var units=await lookups.GetUnitsAsync(token);
            var parsed=Uri.TryCreate(input.Source.Trim(),UriKind.Absolute,out var uri)?RecipeImportParser.ParseStructuredPage(await RecipePageFetcher.FetchAsync(uri,token),catalog,units):RecipeImportParser.ParsePaste(input.Source,catalog,units);
            var recipe=new AiRecipe(parsed.Title??"",parsed.Description??"",parsed.Instructions,parsed.Servings,parsed.PrepMinutes,parsed.CookMinutes,null,
                parsed.Ingredients.Select(i=>new AiIngredient(i.Original,i.IngredientText,i.Quantity,i.IngredientID,i.UnitID,units.FirstOrDefault(u=>u.UnitID==i.UnitID)?.Abbreviation)).ToArray(),input.Source);
            return Results.Ok(recipe);
        });
        api.MapPost("/plans/preview", async (AiPlanApply request,ClaimsPrincipal user,AiService service,RecipeRepository recipes,NutritionRepository nutrition,CancellationToken token)=>{
            if(request.Meals is null||request.Meals.Length is <1 or >84||request.Meals.Any(m=>m.RecipeId is null||!AiValidation.Quantity(m.Portions)))return Results.BadRequest();
            var context=await service.ContextAsync(User(user),request.Meals.Min(m=>m.Date),2,token);
            var preserved=context.Plan.Where(p=>!request.ReplaceEntryIds.Contains(p.MealPlanEntryID)).Select(p=>new AiPlanMeal(DateOnly.FromDateTime(p.PlannedDate),p.CategoryID,p.RecipeID,null,p.UnconsumedPortions,"existing-"+(p.CookingBatchID?.ToString()??p.MealPlanEntryID.ToString()),"",p.IsPrepared?p.CookingBatchID:null)).Where(p=>p.Portions>0).ToArray();
            var previewMeals=preserved.Concat(request.Meals).ToArray();
            var candidates=context.Candidates.ToList();
            foreach(var id in previewMeals.Select(m=>m.RecipeId!.Value).Distinct().Where(id=>!candidates.Any(c=>c.RecipeId==id))){
                var r=await recipes.GetRecipeFullAsync(id);if(r is null)return Results.BadRequest();
                candidates.Add(new(r.RecipeID,r.Title,r.Instructions,r.Servings,r.PrepTimeMinutes,r.CookTimeMinutes,r.CategoryID,r.RowVersion,r.Ingredients.ToArray(),InventoryCalculator.Readiness(r,2,context.Stock,context.UnitMap,request.Meals.Min(m=>m.Date)),await nutrition.GetRecipeNutritionAsync(id)));
            }
            return Results.Ok(AiPlanCalculator.Preview(previewMeals,context with{Candidates=candidates.ToArray()}));
        });
        api.MapPost("/recipes/save", async (AiRecipeSave request, ClaimsPrincipal user, AiDomainRepository domain) => {
            var r = request.Recipe;
            if (request.OperationId == Guid.Empty || r is null || string.IsNullOrWhiteSpace(r.Title) || r.Title.Length >150 || string.IsNullOrWhiteSpace(r.Instructions) || r.Instructions.Length>60000 ||
                r.Description?.Length>12000 || r.Servings is not (>0 and <=1000) || r.PrepTimeMinutes is <0 or >10080 || r.CookTimeMinutes is <0 or >10080 ||
                r.Ingredients is null || r.Ingredients.Count is <1 or >300 || r.Ingredients.Any(i => i.IngredientID<=0 || i.UnitID<=0 || !AiValidation.Quantity(i.Quantity) || i.Notes?.Length>255) ||
                r.Ingredients.Select(i=>i.IngredientID).Distinct().Count()!=r.Ingredients.Count || r.PhotoMode is not ("Keep" or "Delete") || r.ImageData is not null || r.ThumbnailData is not null)
                return Results.BadRequest();
            if(request.Source?.Length>200000||request.SourceKind.Length>30)return Results.BadRequest();
            return Results.Ok(await domain.SaveRecipeAsync(User(user), request));
        });
        api.MapPost("/ingredients", async (AddReviewedIngredient request, ClaimsPrincipal user, AiDomainRepository domain) => {
            if(request.OperationId==Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length>100 || request.UnitId<=0)return Results.BadRequest();
            return Results.Ok(await domain.AddIngredientAsync(User(user),request));
        });
        api.MapGet("/plans", async (DateOnly date, ClaimsPrincipal user, MealPlanRepository plans, CancellationToken token) => Results.Ok(await plans.GetWeeklyPlanAsync(User(user),date.ToDateTime(TimeOnly.MinValue),token)));
        api.MapPost("/plans/apply", async (AiPlanApply request, ClaimsPrincipal user, AiRepository data) => {
            if (request.OperationId==Guid.Empty || request.Meals is null || request.Meals.Length is <1 or >84 || request.ReplaceEntryIds is null || request.ReplaceEntryIds.Length>100 ||
                request.RecipeVersions is null || request.EntryVersions is null || request.BatchVersions is null || request.Meals.Any(m=>m.RecipeId is null || m.DraftIndex is not null || !AiValidation.Quantity(m.Portions)))return Results.BadRequest();
            if(request.JobId is {} id && (await data.JobAsync("get",User(user),id)) is not {State:"Completed",Kind:"plan"})return Results.Conflict();
            return Results.Ok(await data.ApplyPlanAsync(User(user),request));
        });
        api.MapPost("/ai/jobs/{jobId:guid}/purchases", async (Guid jobId, AiReceiptApply request, ClaimsPrincipal user, AiRepository data) => {
            if(request.OperationId==Guid.Empty || request.RowIndexes is null || request.Purchases is null || request.Purchases.Length>200 || request.RowIndexes.Length is <1 or >200 ||
                request.RowIndexes.Distinct().Count()!=request.RowIndexes.Length || request.RowIndexes.Any(i=>i<0||i>=request.Purchases.Length) ||
                request.RowIndexes.Any(i=>!AiValidation.Quantity(request.Purchases[i].Quantity??0) || request.Purchases[i].Name.Length>150))return Results.BadRequest();
            return Results.Ok(await data.AcceptReceiptAsync(User(user),jobId,request));
        });
        api.MapGet("/ai/jobs/{jobId:guid}/purchases",async(Guid jobId,ClaimsPrincipal user,AiRepository data)=>Results.Ok(await data.ReceiptRowsAsync(User(user),jobId)));
    }
}
public sealed class AiDomainRepository(Data.IDbConnectionFactory factory) : InventoryRepository(factory)
{
    public Task<MutationResult> AddIngredientAsync(int userId, AddReviewedIngredient request) => Mutation("sp_AddReviewedIngredient",new {UserID=userId,OperationID=request.OperationId,request.Name,DefaultUnitID=request.UnitId});
    public async Task<RecipeBundleResult> SaveRecipeAsync(int userId,AiRecipeSave request)
    {
        var r=request.Recipe;
        var json=await ExecuteScalarProcAsync<string>("sp_SaveReviewedRecipe",new{UserID=userId,OperationID=request.OperationId,r.RecipeID,r.CategoryID,r.Title,r.Description,r.Instructions,r.PrepTimeMinutes,r.CookTimeMinutes,r.Servings,
            IngredientsJson=System.Text.Json.JsonSerializer.Serialize(r.Ingredients),r.ExpectedVersion,r.PhotoMode,SourceText=request.Source,request.SourceKind});
        return AiJson.Read<RecipeBundleResult>(json);
    }
}
