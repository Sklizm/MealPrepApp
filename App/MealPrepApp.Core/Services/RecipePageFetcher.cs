using System.Net;
using System.Net.Sockets;
using System.Text;
namespace MealPrepApp.Services;
/// <summary>Bounded HTTPS fetch with pinned public DNS results and no proxy/cookie access.</summary>
public static class RecipePageFetcher
{
    public static bool IsPublicAddress(IPAddress address) {
        if (address.IsIPv4MappedToIPv6) address=address.MapToIPv4();
        var b=address.GetAddressBytes();
        if (address.AddressFamily==AddressFamily.InterNetwork) return
            b[0]!=0 && b[0]!=10 && b[0]!=127 && b[0]<224 &&
            !(b[0]==100 && b[1]>=64 && b[1]<=127) && !(b[0]==169 && b[1]==254) &&
            !(b[0]==172 && b[1]>=16 && b[1]<=31) && !(b[0]==192 && (b[1]==168 || (b[1]==0 && b[2] is 0 or 2))) &&
            !(b[0]==198 && (b[1] is 18 or 19 || (b[1]==51&&b[2]==100))) && !(b[0]==203&&b[1]==0&&b[2]==113);
        return address.AddressFamily==AddressFamily.InterNetworkV6 && (b[0]&0xe0)==0x20 &&
            !(b[0]==0x20&&b[1]==0x02) && !(b[0]==0x20&&b[1]==0x01&&b[2]==0&&b[3]==0) &&
            !(b[0]==0x20&&b[1]==0x01&&b[2]==0x0d&&b[3]==0xb8);
    }
    public static void ValidateUri(Uri uri) {
        if (!uri.IsAbsoluteUri || uri.Scheme!=Uri.UriSchemeHttps || uri.Port!=443 || uri.UserInfo.Length>0 ||
            uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".localhost",StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(uri.IdnHost.Trim('[',']'),out var ip)&&!IsPublicAddress(ip))) throw new InvalidDataException(L.Text("UX.PublicHttpsOnly"));
    }
    public static async Task<string> FetchAsync(Uri uri,CancellationToken cancellationToken=default) {
        ValidateUri(uri);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var handler=new SocketsHttpHandler { AllowAutoRedirect=false,UseProxy=false,UseCookies=false,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate,
            ConnectCallback=async (context,token)=> {
                var addresses=await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host,token);
                if (addresses.Length==0 || addresses.Length>32 || addresses.Any(a=>!IsPublicAddress(a))) throw new InvalidDataException(L.Text("UX.PublicHttpsOnly"));
                foreach (var address in addresses) {
                    var socket=new Socket(address.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
                    try { await socket.ConnectAsync(new IPEndPoint(address,443),token); return new NetworkStream(socket,ownsSocket:true); }
                    catch (SocketException) { socket.Dispose(); }
                    catch { socket.Dispose(); throw; }
                }
                throw new HttpRequestException("Recipe host could not be reached.");
            }};
        using var client=new HttpClient(handler); client.DefaultRequestHeaders.UserAgent.ParseAdd("MealPrepApp/1.0");
        for (var redirect=0;redirect<=3;redirect++) {
            ValidateUri(uri);
            using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect) {
                if (response.Headers.Location is not { } location || redirect==3) throw new InvalidDataException(L.Text("UX.UnsupportedImport"));
                uri=new Uri(uri,location); continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength>2*1024*1024) throw new InvalidDataException(L.Text("UX.ImportTooLarge"));
            await using var source=await response.Content.ReadAsStreamAsync(timeout.Token); using var target=new MemoryStream();
            var buffer=new byte[8192]; int count;
            while ((count=await source.ReadAsync(buffer,timeout.Token))>0) { if (target.Length+count>2*1024*1024) throw new InvalidDataException(L.Text("UX.ImportTooLarge")); await target.WriteAsync(buffer.AsMemory(0,count),timeout.Token); }
            return Encoding.UTF8.GetString(target.ToArray());
        }
        throw new InvalidDataException(L.Text("UX.UnsupportedImport"));
    }
}
