using System.Net;
using System.Text;
using System.Text.Json;
using MealPrepApp.Api.Ai;
using MealPrepApp.Models;
using MealPrepApp.Services;
using SkiaSharp;
using Xunit;

namespace MealPrepApp.Tests;

public sealed class AssistantTests
{
    private static readonly DateOnly Day=new(2030,4,1);
    private static AiWork Work(string kind="suggest",AiPreferences? preferences=null)
    {
        var line=new RecipeIngredientLine{IngredientID=1,IngredientName="Rice",UnitID=1,UnitAbbreviation="g",Quantity=100};
        var candidate=new AiCandidate(10,"Rice","Cook rice.",2,5,15,1,[0,0,0,0,0,0,0,1],[line],new(ReadinessStatus.Ready,[]),new(){Servings=2,TotalCalories=360,TotalProteinGrams=7});
        var context=new AiContext(Guid.NewGuid(),[new(){IngredientID=1,Name="Rice"},new(){IngredientID=2,Name="Salt"}],
            [new(){UnitID=1,Name="gram",Abbreviation="g"},new(){UnitID=2,Name="piece",Abbreviation="pc"}],
            [new(1,1,1),new(2,2,1)],[new(){CategoryID=1,Name="Dinner"}],[candidate],
            [new(){BatchID=1,IngredientID=1,UnitID=1,RemainingQuantity=150,ExpiresOn=Day.AddDays(7).ToDateTime(TimeOnly.MinValue),Version=1}],[],[]);
        return new(new(Guid.NewGuid(),kind,"en","Dinner",Day,preferences??new()),context);
    }
    private static AiProposal Suggest(int id=10)=>AiProposal.Empty with{Suggestions=[new(id,"Uses pantry rice.")]};
    [Fact] public void UnknownRecipeIdsCannotBecomeActionableSuggestions()=>Assert.Throws<InvalidDataException>(()=>AiValidation.Proposal(Suggest(999),Work()));
    [Fact] public void ExplicitExclusionsAreCheckedOutsideTheModel()=>Assert.Throws<InvalidDataException>(()=>AiValidation.Proposal(Suggest(),Work(preferences:new(ExcludedIngredientIds:[1]))));
    [Fact] public void UnknownCookingTimesDoNotPassStrictTimeLimits(){var w=Work();w=w with{Context=w.Context with{Candidates=[w.Context.Candidates[0] with{CookMinutes=null}]}};Assert.Throws<InvalidDataException>(()=>AiValidation.Proposal(Suggest(),w));}
    [Fact] public void SuggestionsCannotSmuggleRecipeCreation(){var p=Suggest() with{Recipes=[new("Injected","","Save something.",2,1,1,1,[new("Rice","Rice",1,1,1,"g")])]};Assert.Throws<InvalidDataException>(()=>AiValidation.Proposal(p,Work()));}
    [Fact] public void UnknownIngredientAndUnitRemainUnresolved(){var w=Work("import");var r=new AiRecipe("Recipe","","Cook.",null,null,null,null,[new("some flour","Flour",null,999,999,"unknown")]);var result=AiValidation.Proposal(AiProposal.Empty with{Recipes=[r]},w);Assert.Null(result.Recipes[0].Ingredients[0].IngredientId);Assert.Null(result.Recipes[0].Ingredients[0].UnitId);Assert.Null(result.Recipes[0].Ingredients[0].Quantity);}
    [Fact] public void ReceiptDoesNotEstablishExpiry(){var w=Work("receipt");w=w with{Request=w.Request with{UploadId=Guid.NewGuid()}};var p=AiProposal.Empty with{Purchases=[new("Rice 100 g","Rice",1,100,1,"g",Day,Day.AddDays(1))]};var result=AiValidation.Proposal(p,w);Assert.Null(result.Purchases[0].ExpiresOn);Assert.Equal(1,result.Purchases[0].IngredientId);}
    [Fact] public void ReceiptIngredientIdMustAgreeWithCatalogName(){var p=AiProposal.Empty with{Purchases=[new("Unknown","Unknown",1,1,1,"g",null,null)]};Assert.Null(AiValidation.Proposal(p,Work("receipt")).Purchases[0].IngredientId);}
    [Fact] public void PlanCannotOverwriteUnselectedExistingMeals(){var w=Work("plan");w=w with{Context=w.Context with{Plan=[new(){MealPlanEntryID=1,PlannedDate=Day.ToDateTime(TimeOnly.MinValue),CategoryID=1,RecipeID=10}]}};var p=AiProposal.Empty with{Meals=[new(Day,1,10,null,2,"batch","Dinner")]};Assert.Throws<InvalidDataException>(()=>AiValidation.Proposal(p,w));}
    [Fact] public void BatchKeysCannotCombineDifferentRecipes(){var w=Work("plan");w=w with{Context=w.Context with{Candidates=[w.Context.Candidates[0],w.Context.Candidates[0] with{RecipeId=11}]}};var p=AiProposal.Empty with{Meals=[new(Day,1,10,null,2,"batch",""),new(Day.AddDays(1),1,11,null,2,"batch","")]};Assert.Throws<InvalidDataException>(()=>AiValidation.Proposal(p,w));}
    [Fact] public void PinnedMealsSurvivePartialRegeneration(){var w=Work("plan");var pin=new AiPlanMeal(Day,1,10,null,2,"pinned","Keep");w=w with{Request=w.Request with{PinnedMeals=[pin]}};var p=AiValidation.Proposal(AiProposal.Empty,w);Assert.Equal(pin,Assert.Single(p.Meals));}
    [Fact] public void PinnedMealsCannotBeChangedByTheModel(){var pin=new AiPlanMeal(Day,1,10,null,2,"pinned","Keep");var w=Work("plan");w=w with{Request=w.Request with{PinnedMeals=[pin]}};Assert.Throws<InvalidDataException>(()=>AiValidation.Proposal(AiProposal.Empty with{Meals=[pin with{Portions=3}]},w));}
    [Fact] public void PlanCannotOverAllocatePreparedPortions(){var w=Work("plan");w=w with{Context=w.Context with{Batches=[new(){CookingBatchID=1,RecipeID=10,CookedAt=DateTime.UtcNow,RemainingPortions=3,ReservedPortions=2}]}};var p=AiProposal.Empty with{Meals=[new(Day,1,10,null,2,"prepared","",1)]};Assert.Throws<InvalidDataException>(()=>AiValidation.Proposal(p,w));}
    [Fact] public void SharedBatchShoppingCountsIngredientsOnce(){var w=Work("plan");var p=AiPlanCalculator.Preview([new(Day,1,10,null,1,"batch",""),new(Day.AddDays(1),1,10,null,1,"batch","")],w.Context);Assert.Equal(1,p.CookingBatches);var line=Assert.Single(p.Shopping);Assert.Equal(100,line.Required);Assert.Equal(0,line.Missing);Assert.Equal(360,p.Calories);Assert.Equal(150,w.Context.Stock[0].RemainingQuantity);}
    [Fact] public void DifferentBatchesCannotSpendTheSamePantryStockTwice(){var p=AiPlanCalculator.Preview([new(Day,1,10,null,2,"one",""),new(Day.AddDays(1),1,10,null,2,"two","")],Work("plan").Context);var line=Assert.Single(p.Shopping);Assert.Equal(200,line.Required);Assert.Equal(150,line.FromPantry);Assert.Equal(50,line.Missing);}
    [Fact] public void ExpiredStockDoesNotCountTowardPlanReadiness(){var w=Work("plan");w.Context.Stock[0].ExpiresOn=Day.AddDays(-1).ToDateTime(TimeOnly.MinValue);var line=Assert.Single(AiPlanCalculator.Preview([new(Day,1,10,null,2,"batch","")],w.Context).Shopping);Assert.Equal(100,line.Missing);}
    [Fact] public void ApiDecimalStringsRoundTripThroughSharedContext(){var w=Work();var json=AiJson.Write(w).Replace("\"remainingQuantity\":150","\"remainingQuantity\":\"150.125\"");Assert.Equal(150.125m,AiJson.Read<AiWork>(json).Context.Stock[0].RemainingQuantity);}
    [Fact] public void ImageNormalizationRejectsCorruptInputs()=>Assert.Throws<InvalidDataException>(()=>AiService.NormalizeImage([1,2,3]));
    [Fact] public void ImageNormalizationProducesASeparateJpeg(){using var bitmap=new SKBitmap(30,20);using(var canvas=new SKCanvas(bitmap))canvas.Clear(SKColors.White);using var png=bitmap.Encode(SKEncodedImageFormat.Png,100);var jpeg=AiService.NormalizeImage(png.ToArray());Assert.Equal(0xff,jpeg[0]);Assert.Equal(0xd8,jpeg[1]);}
    [Fact] public async Task DisabledConfigurationNeverContactsAProvider(){var calls=0;var handler=new Handler(_=>{calls++;return new(HttpStatusCode.OK);});var provider=new GeminiProvider(new(),new HttpClient(handler));await Assert.ThrowsAsync<AiProviderException>(()=>provider.GenerateAsync(Work(),null,CancellationToken.None));Assert.Equal(0,calls);}
    [Fact] public async Task QuotaErrorsNeverTriggerPaidFallbackOrAutomaticRetry(){var calls=0;var provider=new GeminiProvider(new(){Enabled=true,FreeTierVerified=true,ApiKey="synthetic"},new HttpClient(new Handler(_=>{calls++;return new(HttpStatusCode.TooManyRequests); })));var e=await Assert.ThrowsAsync<AiProviderException>(()=>provider.GenerateAsync(Work(),null,CancellationToken.None));Assert.Equal("QuotaExceeded",e.Code);Assert.Equal(1,calls);}
    [Fact] public async Task ProviderRequestOmitsIdentityAndUsesStructuredOutput(){string? body=null;var handler=new Handler(request=>{Assert.Equal("generativelanguage.googleapis.com",request.RequestUri!.Host);Assert.True(request.Headers.Contains("x-goog-api-key"));body=request.Content!.ReadAsStringAsync().Result;return new(HttpStatusCode.OK){Content=new StringContent("{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"text\":\"{\\\"suggestions\\\":[],\\\"recipes\\\":[],\\\"meals\\\":[],\\\"purchases\\\":[],\\\"warnings\\\":[]}\"}]}}],\"usageMetadata\":{\"promptTokenCount\":12,\"candidatesTokenCount\":4}}",Encoding.UTF8,"application/json")};});var result=await new GeminiProvider(new(){Enabled=true,FreeTierVerified=true,ApiKey="synthetic"},new HttpClient(handler)).GenerateAsync(Work(),null,CancellationToken.None);Assert.Contains("responseJsonSchema",body);Assert.DoesNotContain("databaseId",body);Assert.DoesNotContain("userId",body);Assert.DoesNotContain("apiKey",body);Assert.Equal(12,result.InputTokens);}
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(respond(request));}
}
