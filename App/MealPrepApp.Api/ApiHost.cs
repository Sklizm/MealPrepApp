using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
namespace MealPrepApp.Api;
public static class ApiHost
{
 public static WebApplication Build(string[] args,IDictionary<string,string?>? configuration=null){
  var builder=WebApplication.CreateBuilder(args);
  builder.Configuration.AddJsonFile("appsettings.Local.json",optional:true);
  if(Environment.GetEnvironmentVariable("MEALPREP_API_SETTINGS") is {} settings)builder.Configuration.AddJsonFile(Path.GetFullPath(settings),optional:false);
  builder.Configuration.AddEnvironmentVariables();if(configuration is not null)builder.Configuration.AddInMemoryCollection(configuration);
  var connection=builder.Configuration.GetConnectionString("MealPrepDB")??throw new InvalidOperationException("Configure ConnectionStrings:MealPrepDB.");
  _=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection);
  var keyText=builder.Configuration["Api:TokenKey"]??throw new InvalidOperationException("Configure a durable 32-byte Api:TokenKey outside Git.");
  byte[] key;try{key=Convert.FromBase64String(keyText);}catch(FormatException){throw new InvalidOperationException("Api:TokenKey must be base64.");}
  if(key.Length<32)throw new InvalidOperationException("Api:TokenKey must contain at least 32 random bytes.");
  var port=builder.Configuration.GetValue("Api:Port",18924);if(port is <0 or >65535)throw new InvalidOperationException("Invalid API port.");
  builder.WebHost.ConfigureKestrel(server=>{server.Listen(IPAddress.Loopback,port);server.Limits.MaxRequestBodySize=65536;server.AddServerHeader=false;});
  builder.Services.ConfigureHttpJsonOptions(options=>options.SerializerOptions.Converters.Add(new DecimalStringConverter()));
  builder.Services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connection));builder.Services.AddSingleton<CompanionRepository>();builder.Services.AddSingleton<ShoppingListRepository>();builder.Services.AddSingleton<InventoryRepository>();builder.Services.AddSingleton<UserRepository>();builder.Services.AddSingleton<IPasswordHasher,BcryptPasswordHasher>();
  builder.Services.AddAuthentication("Device").AddScheme<AuthenticationSchemeOptions,DeviceAuthentication>("Device",_=>{});builder.Services.AddAuthorization();
  builder.Services.AddRateLimiter(options=>{
   options.RejectionStatusCode=429;
   options.AddPolicy("login",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"loopback",_=>new(){PermitLimit=20,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
   options.AddPolicy("account",context=>RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirstValue(ClaimTypes.NameIdentifier)??"anonymous",_=>new(){PermitLimit=180,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
  });
  var app=builder.Build();
  app.Use(async(context,next)=>{
   context.Response.Headers.CacheControl="no-store";
   try{await next();}
   catch(AppDbException ex){
    context.Response.StatusCode=ex.ErrorNumber switch{50004 or 2601 or 2627 or 547=>409,50002=>403,50003=>404,50000=>400,_=>503};
    await context.Response.WriteAsJsonAsync(new{errorCode=context.Response.StatusCode==409?"VersionConflict":context.Response.StatusCode==503?"StorageUnavailable":"RequestRejected"},cancellationToken:context.RequestAborted);
   }
   catch(OperationCanceledException)when(context.RequestAborted.IsCancellationRequested){}
  });
  app.UseAuthentication();app.UseAuthorization();app.UseRateLimiter();
  app.MapGet("/health",()=>Results.Ok(new{status="ok",protocolVersion=1}));
  var dummyHash=new BcryptPasswordHasher().Hash("Companion timing fixture only.");
  app.MapPost("/v1/auth/login",async(LoginRequest input,UserRepository users,IPasswordHasher hasher,CompanionRepository devices)=>{
   if(input.OperationId==Guid.Empty||string.IsNullOrWhiteSpace(input.Identifier)||input.Identifier.Length>255||string.IsNullOrEmpty(input.Password)||input.Password.Length>1024||string.IsNullOrWhiteSpace(input.DeviceName)||input.DeviceName.Length>80)return Results.BadRequest(new{errorCode="InvalidInput"});
   var user=await users.GetUserForLoginAsync(input.Identifier.Trim());
   if(user?.LockedUntil is {} until&&DateTime.SpecifyKind(until,DateTimeKind.Utc)>DateTime.UtcNow)return Results.Json(new{errorCode="AccountLocked",lockedUntil=CompanionRepository.Utc(until)},statusCode:423);
   var verified=false;try{verified=hasher.Verify(input.Password,user?.PasswordHash??dummyHash);}catch(BCrypt.Net.SaltParseException){}
   if(user is null||!verified){await users.RecordLoginFailureAsync(user?.UserID,input.Identifier.Trim());return Results.Json(new{errorCode="InvalidCredentials"},statusCode:401);}
   await users.RecordLoginSuccessAsync(user.UserID);
   var material=Encoding.UTF8.GetBytes($"mealprep-device-v1:{user.UserID.ToString(CultureInfo.InvariantCulture)}:{input.OperationId:D}");
   var token=Convert.ToBase64String(HMACSHA256.HashData(key,material)).TrimEnd('=').Replace('+','-').Replace('/','_');
   var session=await devices.CreateSessionAsync(user.UserID,input.OperationId,SHA256.HashData(Encoding.UTF8.GetBytes(token)),user.PasswordHash,input.DeviceName.Trim());
   if(session.RevokedAt.HasValue||session.ExpiresAt<=DateTime.UtcNow)return Results.Conflict(new{errorCode="NewLoginOperationRequired"});
   return Results.Ok(new LoginResponse(session.SessionID,user.UserID,user.Username,token,CompanionRepository.Utc(session.ExpiresAt)));
  }).RequireRateLimiting("login");
  var api=app.MapGroup("/v1").RequireAuthorization().RequireRateLimiting("account");
  api.MapGet("/operations/{operationId:guid}",async(Guid operationId,ClaimsPrincipal user,CompanionRepository data)=>{
   var saved=await data.OperationAsync(UserId(user),operationId);return saved is null?Results.NotFound():Results.Json(System.Text.Json.JsonDocument.Parse(saved).RootElement.Clone());
  });
  api.MapGet("/sync",async(ClaimsPrincipal user,CompanionRepository data,CancellationToken token)=>Results.Ok(await data.SnapshotAsync(UserId(user),token)));
  api.MapGet("/sessions",async(ClaimsPrincipal user,CompanionRepository data)=>Results.Ok((await data.SessionsAsync(UserId(user))).Select(s=>new{sessionId=s.SessionID,s.DeviceName,createdAt=CompanionRepository.Utc(s.CreatedAt),expiresAt=CompanionRepository.Utc(s.ExpiresAt),revokedAt=s.RevokedAt is {} revoked?CompanionRepository.Utc(revoked):(DateTimeOffset?)null})));
  api.MapPost("/sessions/{sessionId:guid}/revoke",async(Guid sessionId,OperationRequest input,ClaimsPrincipal user,CompanionRepository data)=>input.OperationId==Guid.Empty?Results.BadRequest():Results.Ok(await data.RevokeAsync(UserId(user),input.OperationId,sessionId)));
  api.MapPost("/shopping/lists",async(CreateListRequest input,ClaimsPrincipal user,CompanionRepository data)=>input.OperationId==Guid.Empty||input.EndDate<input.StartDate?Results.BadRequest():Results.Ok(await data.CreateListAsync(UserId(user),input)));
  api.MapPost("/shopping/items",async(ShoppingMutation input,ClaimsPrincipal user,ShoppingListRepository data)=>!Valid(input)||input.ExpectedVersion!=0?Results.BadRequest(new{errorCode="InvalidInput"}):Results.Ok(await data.ChangeItemAsync(UserId(user),input.OperationId,input.ToChange(null))));
  api.MapPut("/shopping/items/{itemId:int}",async(int itemId,ShoppingMutation input,ClaimsPrincipal user,ShoppingListRepository data)=>!Valid(input)||input.ExpectedVersion<1?Results.BadRequest(new{errorCode="InvalidInput"}):Results.Ok(await data.ChangeItemAsync(UserId(user),input.OperationId,input.ToChange(itemId))));
  api.MapPost("/shopping/items/{itemId:int}/import",async(int itemId,VersionedRequest input,ClaimsPrincipal user,ShoppingListRepository data)=>input.OperationId==Guid.Empty||input.ExpectedVersion<1?Results.BadRequest():Results.Ok(await data.ImportPurchaseAsync(UserId(user),input.OperationId,itemId,input.ExpectedVersion)));
  api.MapGet("/pantry",async(ClaimsPrincipal user,CompanionRepository data,CancellationToken token)=>Results.Ok((await data.SnapshotAsync(UserId(user),token)).Batches));
  api.MapPost("/pantry/batches",async(AddStock input,ClaimsPrincipal user,InventoryRepository data)=>input.OperationId==Guid.Empty||!Quantity(input.Quantity)||input.ExpiresOn<input.PurchasedOn?Results.BadRequest():Results.Ok(await data.AddAsync(UserId(user),input.OperationId,input.IngredientId,input.UnitId,input.Quantity,input.PurchasedOn?.ToDateTime(TimeOnly.MinValue),input.ExpiresOn?.ToDateTime(TimeOnly.MinValue))));
  api.MapPost("/pantry/batches/{batchId:int}/adjust",async(int batchId,StockAdjustment input,ClaimsPrincipal user,InventoryRepository data)=>input.OperationId==Guid.Empty||input.ExpectedVersion<1||input.Delta==0||!Quantity(Math.Abs(input.Delta))||input.Kind is not("Correction" or "Discard")||(input.Kind=="Discard"&&input.Delta>0)?Results.BadRequest():Results.Ok(await data.AdjustAsync(UserId(user),input.OperationId,batchId,input.ExpectedVersion,input.Delta,input.Kind)));
  api.MapPut("/pantry/thresholds/{ingredientId:int}",async(int ingredientId,SetThreshold input,ClaimsPrincipal user,InventoryRepository data)=>input.OperationId==Guid.Empty||input.ExpectedVersion<0||input.Quantity<0||input.Quantity>99999999||decimal.Round(input.Quantity,4)!=input.Quantity?Results.BadRequest():Results.Ok(await data.SetThresholdAsync(UserId(user),input.OperationId,ingredientId,input.UnitId,input.Quantity,input.ExpectedVersion)));
  api.MapGet("/catalog",async(ClaimsPrincipal user,CompanionRepository data,CancellationToken token)=>{var all=await data.SnapshotAsync(UserId(user),token);return Results.Ok(new{all.Ingredients,all.Units,all.UnitMap});});
  return app;
 }
 private static int UserId(ClaimsPrincipal user)=>int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!,CultureInfo.InvariantCulture);
 private static bool Quantity(decimal value)=>value>0&&value<=99999999&&decimal.Round(value,4)==value;
 private static bool Valid(ShoppingMutation input)=>input.OperationId!=Guid.Empty&&input.SnapshotId>0&&!string.IsNullOrWhiteSpace(input.Name)&&input.Name.Length<=150&&Quantity(input.RequiredQuantity)&&(!input.PurchasedQuantity.HasValue||Quantity(input.PurchasedQuantity.Value))&&!(input.ExpiresOn<input.PurchasedOn);
}
