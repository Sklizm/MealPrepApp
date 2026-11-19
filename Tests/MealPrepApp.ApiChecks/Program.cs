using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MealPrepApp.Api;
using MealPrepApp.Api.Ai;
using Microsoft.Extensions.DependencyInjection;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using SkiaSharp;
var connection=Environment.GetEnvironmentVariable("MEALPREP_TEST_CONNECTION")??throw new InvalidOperationException("Disposable database connection required.");
var container=Environment.GetEnvironmentVariable("MEALPREP_TEST_CONTAINER")??"";
if(!container.StartsWith("mealprep-api-test-",StringComparison.Ordinal))throw new InvalidOperationException("A disposable API test container is required.");
var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);json.Converters.Add(new DecimalStringConverter());
var checks=0;void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
await using var app=ApiHost.Build([],new Dictionary<string,string?>{["ConnectionStrings:MealPrepDB"]=connection,["Api:TokenKey"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),["Api:Port"]="0",["Ai:Enabled"]="true",["Ai:FreeTierVerified"]="true",["Ai:ApiKey"]="synthetic-not-a-provider-key"},services=>services.AddSingleton<IAiProvider,FixtureAiProvider>());
await app.StartAsync();
try{
 using var client=new HttpClient{BaseAddress=new Uri(app.Urls.Single()),Timeout=TimeSpan.FromSeconds(20)};
 var users=new UserRepository(new SqlConnectionFactory(connection));var password="Fixture-password-Only-382!";var hasher=new BcryptPasswordHasher();var hash=hasher.Hash(password);
 var aId=await users.RegisterUserAsync("api-fixture-a","a@api.example.invalid",hash);var bId=await users.RegisterUserAsync("api-fixture-b","b@api.example.invalid",hash);
 async Task<HttpResponseMessage> Send(HttpMethod method,string path,object? body,string? token=null){using var request=new HttpRequestMessage(method,path);if(token is not null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);if(body is not null)request.Content=JsonContent.Create(body,options:json);return await client.SendAsync(request);}
 async Task<T> Read<T>(HttpResponseMessage response){Check(response.IsSuccessStatusCode,"HTTP "+(int)response.StatusCode);return (await response.Content.ReadFromJsonAsync<T>(json))!;}
 async Task<LoginResponse> Login(string username,Guid operation,string device){using var result=await Send(HttpMethod.Post,"/v1/auth/login",new LoginRequest(operation,username,password,device));return await Read<LoginResponse>(result);}
 using(var response=await Send(HttpMethod.Get,"/v1/sync",null))Check(response.StatusCode==HttpStatusCode.Unauthorized,"anonymous sync rejected");
 var loginOp=Guid.NewGuid();var a=await Login("api-fixture-a",loginOp,"Fixture phone");var again=await Login("api-fixture-a",loginOp,"Fixture phone");Check(a.Token==again.Token&&a.SessionId==again.SessionId&&a.ExpiresAt==again.ExpiresAt,"login retry returns original opaque session");
 var a2=await Login("api-fixture-a",Guid.NewGuid(),"Second fixture phone");var b=await Login("api-fixture-b",Guid.NewGuid(),"Other account phone");Check(a.UserId==aId&&b.UserId==bId,"existing account verification");
 async Task<SyncResponse> Sync(string token){using var response=await Send(HttpMethod.Get,"/v1/sync",null,token);return await Read<SyncResponse>(response);}
 var initial=await Sync(a.Token);Check(initial.Ingredients.Count>0&&initial.Units.Count>0&&initial.UnitMap.Count>0,"catalog and exact conversion snapshot");
 var today=DateOnly.FromDateTime(DateTime.Today);var listRequest=new CreateListRequest(Guid.NewGuid(),today,today.AddDays(6));
 using var listResult=await Send(HttpMethod.Post,"/v1/shopping/lists",listRequest,a.Token);var list=await Read<MutationResult>(listResult);
 using(var retry=await Send(HttpMethod.Post,"/v1/shopping/lists",listRequest,a.Token))Check((await Read<MutationResult>(retry)).Id==list.Id,"list create idempotency");
 var free=new ShoppingMutation(Guid.NewGuid(),0,list.Id,"Unmapped fixture grocery",null,null,1,true,1,null,null,null);
 using var freeResult=await Send(HttpMethod.Post,"/v1/shopping/items",free,a.Token);var freeItem=await Read<MutationResult>(freeResult);
 using(var retry=await Send(HttpMethod.Post,"/v1/shopping/items",free,a.Token))Check((await Read<MutationResult>(retry)).Id==freeItem.Id,"manual-add retry is idempotent");
 using(var rejected=await Send(HttpMethod.Post,$"/v1/shopping/items/{freeItem.Id}/import",new VersionedRequest(Guid.NewGuid(),1),a.Token))Check(rejected.StatusCode==HttpStatusCode.BadRequest,"unmapped purchase cannot import");
 var ingredient=initial.Ingredients.First();var ml=initial.Units.Single(u=>u.Abbreviation=="ml").UnitId;var litre=initial.Units.Single(u=>u.Abbreviation=="l").UnitId;
 var purchase=new ShoppingMutation(Guid.NewGuid(),0,list.Id,"Liquid fixture",ingredient.IngredientId,ml,200,true,1,litre,null,today.AddDays(2));
 using var purchaseResult=await Send(HttpMethod.Post,"/v1/shopping/items",purchase,a.Token);var item=await Read<MutationResult>(purchaseResult);
 var changed=purchase with{OperationId=Guid.NewGuid(),ExpectedVersion=1,Name="Updated liquid fixture"};
 using(var result=await Send(HttpMethod.Put,$"/v1/shopping/items/{item.Id}",changed,a.Token))Check((await Read<MutationResult>(result)).Version==2,"versioned item update");
 using(var conflict=await Send(HttpMethod.Put,$"/v1/shopping/items/{item.Id}",changed with{OperationId=Guid.NewGuid(),IsPurchased=false},a2.Token))Check(conflict.StatusCode==HttpStatusCode.Conflict,"second-device conflict is explicit");
 using(var foreign=await Send(HttpMethod.Put,$"/v1/shopping/items/{item.Id}",changed with{OperationId=Guid.NewGuid()},b.Token))Check(foreign.StatusCode==HttpStatusCode.Forbidden,"shopping ownership enforced on server");
 var import=new VersionedRequest(Guid.NewGuid(),2);using(var imported=await Send(HttpMethod.Post,$"/v1/shopping/items/{item.Id}/import",import,a.Token))Check((await Read<MutationResult>(imported)).Version==3,"reviewed purchase imported");
 using(var repeated=await Send(HttpMethod.Post,$"/v1/shopping/items/{item.Id}/import",import,a.Token))Check((await Read<MutationResult>(repeated)).Version==3,"import replay returns original result");
 var saved=await Sync(a.Token);var batch=saved.Batches.Single();Check(batch.RemainingQuantity==1&&batch.UnitId==litre,"purchased litre kept distinct from 200 ml requirement");Check(batch.PurchasedOn is null&&batch.ExpiresOn==today.AddDays(2)&&batch.UpdatedAt.Offset==TimeSpan.Zero,"unknown dates preserved and UTC event transport");
 using(var request=await Send(HttpMethod.Put,$"/v1/shopping/items/{item.Id}",changed with{OperationId=Guid.NewGuid(),ExpectedVersion=3,PurchasedQuantity=2},a.Token))Check(request.StatusCode==HttpStatusCode.Conflict,"imported purchase cannot rewrite inventory");
 var adjustment=new StockAdjustment(Guid.NewGuid(),batch.Version,.125m);using(var adjusted=await Send(HttpMethod.Post,$"/v1/pantry/batches/{batch.BatchId}/adjust",adjustment,a.Token))await Read<MutationResult>(adjusted);
 using(var repeated=await Send(HttpMethod.Post,$"/v1/pantry/batches/{batch.BatchId}/adjust",adjustment,a.Token))await Read<MutationResult>(repeated);
 Check((await Sync(a.Token)).Batches.Single().RemainingQuantity==1.125m,"decimal correction idempotency");
 using(var foreign=await Send(HttpMethod.Post,$"/v1/pantry/batches/{batch.BatchId}/adjust",adjustment with{OperationId=Guid.NewGuid(),ExpectedVersion=2},b.Token))Check(foreign.StatusCode==HttpStatusCode.Forbidden,"pantry ownership enforced");
 using(var threshold=await Send(HttpMethod.Put,$"/v1/pantry/thresholds/{ingredient.IngredientId}",new SetThreshold(Guid.NewGuid(),0,litre,2),a.Token))await Read<MutationResult>(threshold);
 var inventory=new InventoryRepository(new SqlConnectionFactory(connection));var summary=await inventory.GetSummaryAsync(a.UserId,DateTime.Today);Check(summary.LowStockIngredients==1&&summary.ExpiringBatches==1,"opt-in thresholds and expiry summaries use eligible units");
 var deleted=free with{OperationId=Guid.NewGuid(),ExpectedVersion=1,IsDeleted=true};using(var deletion=await Send(HttpMethod.Put,$"/v1/shopping/items/{freeItem.Id}",deleted,a.Token))await Read<MutationResult>(deletion);
 Check((await Sync(a.Token)).Items.Single(i=>i.ItemId==freeItem.Id).IsDeleted,"deleted items retain synchronization identity");
 var isolated=await Sync(b.Token);Check(isolated.Lists.Count==0&&isolated.Items.Count==0&&isolated.Batches.Count==0&&isolated.Thresholds.Count==0,"account data isolation");
 using(var raw=await Send(HttpMethod.Get,"/v1/sync",null,a.Token)){using var document=JsonDocument.Parse(await raw.Content.ReadAsStringAsync());Check(document.RootElement.GetProperty("batches")[0].GetProperty("remainingQuantity").ValueKind==JsonValueKind.String,"decimal transport uses exact strings");}
 // AI tests always use a local fake provider; CI never spends a model quota.
 using(var capabilities=await Send(HttpMethod.Get,"/v1/ai/capabilities",null,a.Token))Check((await Read<AiCapabilities>(capabilities)).FreeOnly,"assistant advertises free-only configuration");
 using(var accepted=await Send(HttpMethod.Post,"/v1/ai/consent",new AiConsent("gemini-free-1",true),a.Token))Check(accepted.IsSuccessStatusCode,"assistant consent recorded");
 var aiRequest=new AiRequest(Guid.NewGuid(),"suggest","en","Synthetic dinner",today,new AiPreferences());
 using(var missingConsent=await Send(HttpMethod.Post,"/v1/ai/jobs",aiRequest with{OperationId=Guid.NewGuid()},b.Token))Check(missingConsent.StatusCode==HttpStatusCode.Forbidden,"assistant requires each account's consent");
 AiJob aiJob;using(var submitted=await Send(HttpMethod.Post,"/v1/ai/jobs",aiRequest,a.Token))aiJob=await Read<AiJob>(submitted);
 using(var replayed=await Send(HttpMethod.Post,"/v1/ai/jobs",aiRequest,a.Token))Check((await Read<AiJob>(replayed)).JobId==aiJob.JobId,"job submission retries preserve identity");
 using(var foreignJob=await Send(HttpMethod.Get,$"/v1/ai/jobs/{aiJob.JobId}",null,b.Token))Check(foreignJob.StatusCode==HttpStatusCode.NotFound,"AI results remain account owned");
 for(var attempt=0;attempt<30&&aiJob.State is "Queued" or "Running";attempt++){await Task.Delay(250);using var response=await Send(HttpMethod.Get,$"/v1/ai/jobs/{aiJob.JobId}",null,a.Token);aiJob=await Read<AiJob>(response);}
 Check(aiJob.State=="Completed"&&AiJson.Read<AiProposal>(aiJob.ResultJson!).Suggestions.Length==0,"fake-provider job completes without external requests");
 AiContext assistantContext;
 using(var contextResult=await Send(HttpMethod.Get,$"/v1/ai/context?date={today:yyyy-MM-dd}&servings=2",null,a.Token))
 {
  assistantContext=await Read<AiContext>(contextResult);
  Check(assistantContext.DatabaseId!=Guid.Empty,"AI context carries stable deployment identity");
 }
 var slotId=assistantContext.Categories.First().CategoryID;
 var recipeInput=new RecipeBundleInput(null,slotId,"Reviewed API recipe","Synthetic source","Heat water.",
  1,2,2,[new RecipeIngredientInput{IngredientID=ingredient.IngredientId,UnitID=ml,Quantity=0.3333m}],null,"Keep",null,null);
 var recipeSave=new AiRecipeSave(Guid.NewGuid(),recipeInput,"Synthetic recipe provenance","import");
 async Task CheckRecipeRejection(RecipeBundleInput rejected, string code)
 {
  using var response = await Send(HttpMethod.Post, "/v1/recipes/save", recipeSave with { Recipe = rejected }, a.Token);
  using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
  Check(response.StatusCode == HttpStatusCode.BadRequest && problem.RootElement.GetProperty("errorCode").GetString() == code, code);
 }
 await CheckRecipeRejection(recipeInput with { Servings = null }, "RecipeServingsRequired");
 await CheckRecipeRejection(recipeInput with
 {
  Ingredients = [recipeInput.Ingredients[0], new RecipeIngredientInput { IngredientID = ingredient.IngredientId, UnitID = litre, Quantity = 1 }]
 }, "DuplicateRecipeIngredient");
 await CheckRecipeRejection(recipeInput with { PhotoMode = "Set", ImageData = [1, 2, 3] }, "InvalidRecipePhoto");
 using var sampleImage = new SKBitmap(640, 480);
 sampleImage.Erase(SKColors.Green);
 using var importedJpeg = sampleImage.Encode(SKEncodedImageFormat.Jpeg, 85);
 recipeSave = recipeSave with { Recipe = recipeInput with { PhotoMode = "Set", ImageData = importedJpeg.ToArray() } };
 RecipeBundleResult recipe;
 using (var ignored = await Send(HttpMethod.Post, "/v1/recipes/save", recipeSave, a.Token))
  Check(ignored.IsSuccessStatusCode, "recipe save completed before its response was discarded");
 using(var replay=await Send(HttpMethod.Post,"/v1/recipes/save",recipeSave,a.Token))
  recipe=await Read<RecipeBundleResult>(replay);
 using (var matches = await Send(HttpMethod.Get, "/v1/recipes?search=Reviewed%20API%20recipe", null, a.Token))
  Check((await Read<RecipeListItem[]>(matches)).Single().RecipeID == recipe.Id, "discarded recipe save response recovers one recipe");
 using (var detail = await Send(HttpMethod.Get, $"/v1/recipes/{recipe.Id}", null, a.Token))
 {
  var rebuilt = await Read<RecipeFull>(detail);
  Check(rebuilt.Ingredients.Single().Quantity == 0.3333m, "reopened recipe retains four decimal places");
  Check(rebuilt.Ingredients.Single().Quantity * ServingCalculator.Factor(rebuilt.Servings, 4) == 0.6666m,
   "reopened exact quantity scales without display rounding");
 }
 var recipeRepository = new RecipeRepository(new SqlConnectionFactory(connection));
 var savedPhoto = await recipeRepository.GetRecipePhotoAsync(recipe.Id);
 using var savedBitmap = SKBitmap.Decode(savedPhoto?.ImageData);
 Check(savedPhoto?.ContentType == "image/jpeg" && savedBitmap.Width == 640 && savedBitmap.Height == 480,
  "reviewed recipe and imported photo save together");
 sampleImage.Erase(SKColors.Red);
 using var differentJpeg = sampleImage.Encode(SKEncodedImageFormat.Jpeg, 85);
 using (var changedPhoto = await Send(HttpMethod.Post, "/v1/recipes/save",
  recipeSave with { Recipe = recipeSave.Recipe with { ImageData = differentJpeg.ToArray() } }, a.Token))
  Check(changedPhoto.StatusCode == HttpStatusCode.Conflict, "retry cannot reuse the operation ID for a different recipe photo");
 var mealDate=today.AddDays(1);
 var applyPlan=new AiPlanApply(Guid.NewGuid(),null,[new AiPlanMeal(mealDate,slotId,recipe.Id,null,2,"fixture","")],[],
  new(){{recipe.Id,"0x"+Convert.ToHexString(recipe.Version)}},new(),new());
 var unitMapping = assistantContext.UnitMap.FirstOrDefault(unit => unit.UnitID == ml);
 using(var preview=await Send(HttpMethod.Post,"/v1/plans/preview",applyPlan,a.Token))
  Check((await Read<AiPlanPreview>(preview)).Shopping.Any(line => line.IngredientId == ingredient.IngredientId &&
   line.Required == 0.3333m * (unitMapping?.Multiplier ?? 1m)), "plan preview uses exact ingredient quantities");
 using(var stale=await Send(HttpMethod.Post,"/v1/plans/apply",
  applyPlan with{OperationId=Guid.NewGuid(),RecipeVersions=new(){{recipe.Id,"0x0000000000000000"}}},a.Token))
  Check(stale.StatusCode==HttpStatusCode.Conflict,"reviewed plan rejects stale recipe version");
 using(var applied=await Send(HttpMethod.Post,"/v1/plans/apply",applyPlan,a.Token))
  Check((await Read<MutationResult>(applied)).Id==1,"reviewed plan applies through the API");
 using(var replay=await Send(HttpMethod.Post,"/v1/plans/apply",applyPlan,a.Token))
  Check((await Read<MutationResult>(replay)).Id==1,"reviewed plan apply is idempotent");
 using(var planned=await Send(HttpMethod.Get,$"/v1/plans?date={mealDate:yyyy-MM-dd}",null,a.Token))
  Check((await Read<MealPlanEntry[]>(planned)).Single().RecipeID==recipe.Id,"only one reviewed meal exists after retry");
 Check((await Sync(a.Token)).Batches.Single().RemainingQuantity==1.125m,"reviewed plan leaves pantry stock available");
 using(var revokeForeign=await Send(HttpMethod.Post,$"/v1/sessions/{a.SessionId}/revoke",new OperationRequest(Guid.NewGuid()),b.Token))Check(revokeForeign.StatusCode==HttpStatusCode.Forbidden,"device-session ownership enforced");
 using(var revoke=await Send(HttpMethod.Post,$"/v1/sessions/{a2.SessionId}/revoke",new OperationRequest(Guid.NewGuid()),a.Token))await Read<MutationResult>(revoke);
 using(var revoked=await Send(HttpMethod.Get,"/v1/sync",null,a2.Token))Check(revoked.StatusCode==HttpStatusCode.Unauthorized,"revocation takes effect immediately");
 for(var n=0;n<5;n++){using var bad=await Send(HttpMethod.Post,"/v1/auth/login",new LoginRequest(Guid.NewGuid(),"api-fixture-b","Wrong fixture password","Fixture"));Check(bad.StatusCode==HttpStatusCode.Unauthorized,"failed password remains unauthorized");}
 using(var locked=await Send(HttpMethod.Post,"/v1/auth/login",new LoginRequest(Guid.NewGuid(),"api-fixture-b",password,"Fixture")))Check((int)locked.StatusCode==423,"existing account lockout reused");
 var expiry=await Login("api-fixture-a",Guid.NewGuid(),"Expiry fixture");
 var sqlInfo=new System.Diagnostics.ProcessStartInfo("docker"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
 foreach(var argument in new[]{"exec","-i",container,"sh","-c","export SQLCMDPASSWORD=\"$MSSQL_SA_PASSWORD\"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q \"$1\"","sh",$"USE MealPrepDB; UPDATE dbo.DeviceSessions SET ExpiresAt=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE SessionID='{expiry.SessionId:D}';"})sqlInfo.ArgumentList.Add(argument);
 using(var process=System.Diagnostics.Process.Start(sqlInfo)!){await process.WaitForExitAsync();Check(process.ExitCode==0,"disposable session expiry fixture");}
 using(var expired=await Send(HttpMethod.Get,"/v1/sync",null,expiry.Token))Check(expired.StatusCode==HttpStatusCode.Unauthorized,"expired device session rejected");
 using(var operation=await Send(HttpMethod.Get,$"/v1/operations/{import.OperationId:D}",null,a.Token))Check((await Read<MutationResult>(operation)).Id==item.Id,"lost mutation responses can be recovered by operation ID");
 using(var foreignOperation=await Send(HttpMethod.Get,$"/v1/operations/{import.OperationId:D}",null,b.Token))Check(foreignOperation.StatusCode==HttpStatusCode.NotFound,"operation-result ownership enforced");
 await users.ChangePasswordAsync(a.UserId,hasher.Hash("Changed-fixture-password-only!"),hash);
 using(var invalidated=await Send(HttpMethod.Get,"/v1/sync",null,a.Token))Check(invalidated.StatusCode==HttpStatusCode.Unauthorized,"password changes invalidate device sessions");
 Console.WriteLine($"{checks} API integration checks passed against disposable SQL Server.");
}finally{await app.StopAsync();}

sealed class FixtureAiProvider:IAiProvider
{
 public Task<AiProviderResult> GenerateAsync(AiWork work,byte[]? image,CancellationToken token)=>Task.FromResult(new AiProviderResult(AiProposal.Empty,12,4));
}
