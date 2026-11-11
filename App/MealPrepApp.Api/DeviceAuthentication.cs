using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
namespace MealPrepApp.Api;
public sealed class DeviceAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,CompanionRepository repository)
 :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
 protected override async Task<AuthenticateResult> HandleAuthenticateAsync(){
  if(!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization,out var header)||!header.Scheme.Equals("Bearer",StringComparison.OrdinalIgnoreCase)||header.Parameter is not {} token)return AuthenticateResult.NoResult();
  if(token.Length!=43||token.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_')))return AuthenticateResult.Fail("Invalid session.");
  var session=await repository.ResolveAsync(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
  if(session is null)return AuthenticateResult.Fail("Expired or revoked session.");
  var identity=new ClaimsIdentity([new(ClaimTypes.NameIdentifier,session.UserID.ToString(System.Globalization.CultureInfo.InvariantCulture)),new(ClaimTypes.Name,session.Username),new("session_id",session.SessionID.ToString())],Scheme.Name);
  return AuthenticateResult.Success(new(new ClaimsPrincipal(identity),Scheme.Name));
 }
}
