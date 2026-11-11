using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MealPrepApp.Api;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
var connection=Environment.GetEnvironmentVariable("MEALPREP_TEST_CONNECTION")??throw new InvalidOperationException("Disposable database connection required.");
var container=Environment.GetEnvironmentVariable("MEALPREP_TEST_CONTAINER")??"";
if(!container.StartsWith("mealprep-api-test-",StringComparison.Ordinal))throw new InvalidOperationException("A disposable API test container is required.");
var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);json.Converters.Add(new DecimalStringConverter());
var checks=0;void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;Console.WriteLine("PASS "+name);}
await using var app=ApiHost.Build([],new Dictionary<string,string?>{["ConnectionStrings:MealPrepDB"]=connection,["Api:TokenKey"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),["Api:Port"]="0"});
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
