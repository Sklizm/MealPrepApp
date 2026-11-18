using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;

namespace MealPrepApp.Services;

public interface IApiSession
{
    Task TrySignInAsync(string identifier, string password, int userId);
    void Clear();
}
public sealed class AiClientException(string code, int? status = null) : Exception(code) { public string Code { get; } = code; public int? Status {get;}=status; }
public sealed class AiClient(AiRepository identity) : IApiSession, IDisposable
{
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private string? _token; private Guid? _sessionId;
    private readonly LocalJsonStore _settings = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MealPrepApp", "assistant-connection.json"));
    public string Server { get; private set; } = "";
    public string? AccountKey { get; private set; }
    public bool Connected => _token is not null;
    public string LastStatus { get; private set; } = "Disconnected";
    public string SavedServer => _settings.Read<Dictionary<string,string>>()?.GetValueOrDefault("server") ?? "";
    public async Task TrySignInAsync(string identifier, string password, int userId)
    {
        var server = SavedServer;
        if (server.Length == 0) return;
        try { await ConnectAsync(server, identifier, password, userId); }
        catch { Clear(); LastStatus = "ConnectionRequired"; }
    }
    public async Task ConnectAsync(string server, string identifier, string password, int userId)
    {
        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || uri.Query.Length>0 || uri.Fragment.Length>0)
            throw new AiClientException("ConnectionRequired");
        Clear(); Server = server.TrimEnd('/');
        var login = await CallAsync<JsonElement>("POST", "/v1/auth/login", new { operationId = Guid.NewGuid(), identifier, password, deviceName = "MealPrepApp Desktop" });
        if (login.GetProperty("userId").GetInt32()!=userId) throw new AiClientException("WrongDatabase");
        _token = login.GetProperty("token").GetString(); _sessionId = login.GetProperty("sessionId").GetGuid();
        try {
            var database = await CallAsync<JsonElement>("GET", "/v1/identity");
            var databaseId = await identity.DatabaseIdAsync();
            if (database.GetProperty("databaseId").GetGuid()!=databaseId) throw new AiClientException("WrongDatabase");
            AccountKey = $"{databaseId:N}-{userId}";
            _settings.Write(new Dictionary<string,string>{{"server",Server}}); LastStatus = "Connected";
        } catch { Clear(); throw; }
    }
    public async Task<T> CallAsync<T>(string method, string path, object? body = null, CancellationToken token = default)
    {
        if (Server.Length == 0) throw new AiClientException("ConnectionRequired");
        using var message = new HttpRequestMessage(new HttpMethod(method), Server + path);
        if (_token is not null) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (body is not null) message.Content = JsonContent.Create(body, options: AiJson.Options);
        using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode)
        {
            var code = (int)response.StatusCode switch {401=>"SessionExpired",403=>"ConsentRequired",409=>"VersionConflict",429=>"QuotaExceeded",_=>"Unavailable"};
            if ((int)response.StatusCode==401) Clear();
            throw new AiClientException(code,(int)response.StatusCode);
        }
        await using var input=await response.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=await input.ReadAsync(buffer,token))>0){if(output.Length+count>16*1024*1024)throw new AiClientException("InvalidResponse");await output.WriteAsync(buffer.AsMemory(0,count),token);}
        return AiJson.Read<T>(Encoding.UTF8.GetString(output.ToArray()));
    }
    public async Task<Guid> UploadAsync(byte[] image, CancellationToken token=default)
    {
        if(!Connected)throw new AiClientException("ConnectionRequired");
        using var message=new HttpRequestMessage(HttpMethod.Post,Server+"/v1/ai/uploads");message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",_token);
        message.Headers.Add("X-Image-Reviewed","true");message.Content=new ByteArrayContent(image);message.Content.Headers.ContentType=new MediaTypeHeaderValue("image/jpeg");
        using var response=await _http.SendAsync(message,token);if(!response.IsSuccessStatusCode)throw new AiClientException("UploadFailed");
        var result=await response.Content.ReadFromJsonAsync<JsonElement>(token);return result.GetProperty("uploadId").GetGuid();
    }
    public void Clear() { _token=null;_sessionId=null;AccountKey=null;LastStatus="Disconnected"; }
    public void Dispose()=>_http.Dispose();
}
