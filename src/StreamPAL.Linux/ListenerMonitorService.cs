using System.Net.Http;
using System.Text.Json;

namespace StreamPAL.Linux;

public sealed class ListenerMonitorService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<int> ReadAsync(EncoderProfile encoder, CancellationToken token = default) =>
        encoder.ServerType == "Icecast 2" ? await ReadIcecastAsync(encoder, token) : await ReadShoutcastAsync(encoder, token);

    private async Task<int> ReadShoutcastAsync(EncoderProfile encoder, CancellationToken token)
    {
        using var document = JsonDocument.Parse(await _http.GetStringAsync($"http://{encoder.Host}:{encoder.Port}/statistics?json=1", token));
        var root = document.RootElement;
        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            var wanted = NormalizeMount(encoder.Mount);
            foreach (var stream in streams.EnumerateArray())
            {
                var path = stream.TryGetProperty("streampath", out var p) ? NormalizeMount(p.GetString()) : "";
                if ((path == wanted || streams.GetArrayLength() == 1) && stream.TryGetProperty("currentlisteners", out var listeners)) return listeners.GetInt32();
            }
        }
        return root.TryGetProperty("currentlisteners", out var total) ? total.GetInt32() : 0;
    }

    private async Task<int> ReadIcecastAsync(EncoderProfile encoder, CancellationToken token)
    {
        using var document = JsonDocument.Parse(await _http.GetStringAsync($"http://{encoder.Host}:{encoder.Port}/status-json.xsl", token));
        if (!document.RootElement.TryGetProperty("icestats", out var stats) || !stats.TryGetProperty("source", out var source)) return 0;
        var sources = source.ValueKind == JsonValueKind.Array ? source.EnumerateArray().ToArray() : [source];
        var wanted = NormalizeMount(encoder.Mount);
        foreach (var item in sources)
        {
            var listenUrl = item.TryGetProperty("listenurl", out var u) ? u.GetString() ?? "" : "";
            var path = Uri.TryCreate(listenUrl, UriKind.Absolute, out var uri) ? NormalizeMount(uri.AbsolutePath) : NormalizeMount(listenUrl);
            if ((path == wanted || sources.Length == 1) && item.TryGetProperty("listeners", out var listeners)) return listeners.GetInt32();
        }
        return 0;
    }

    private static string NormalizeMount(string? mount) => "/" + (mount ?? "").Trim().TrimStart('/').TrimEnd('/').ToLowerInvariant();
    public void Dispose() => _http.Dispose();
}
