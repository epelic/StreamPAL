using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using StreamForge.Models;
namespace StreamForge.Services;
public sealed class MetadataUpdateService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    public async Task UpdateAsync(EncoderProfile encoder, CancellationToken token = default)
    {
        var song = Uri.EscapeDataString(encoder.Metadata ?? ""); string url; using var request = new HttpRequestMessage(HttpMethod.Get, "about:blank");
        if (encoder.ServerType == "Icecast 2") { var mount = Uri.EscapeDataString(encoder.Mount.StartsWith('/') ? encoder.Mount : "/" + encoder.Mount); url = $"http://{encoder.Host}:{encoder.Port}/admin/metadata?mount={mount}&mode=updinfo&song={song}"; request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"source:{encoder.Password}"))); }
        else url = $"http://{encoder.Host}:{encoder.Port}/admin.cgi?pass={Uri.EscapeDataString(encoder.Password)}&mode=updinfo&song={song}";
        request.RequestUri = new Uri(url); using var response = await _http.SendAsync(request, token); response.EnsureSuccessStatusCode();
    }
}
