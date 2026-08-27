using System.Net.Http;
using System.Text.Json;
using StreamForge.Models;

namespace StreamForge.Services;

public sealed class ListenerMonitorService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<int> ReadAsync(EncoderProfile encoder, CancellationToken token = default)
    {
        return encoder.ServerType == "Icecast 2"
            ? await ReadIcecastAsync(encoder, token)
            : await ReadShoutcastAsync(encoder, token);
    }

    private async Task<int> ReadShoutcastAsync(EncoderProfile encoder, CancellationToken token)
    {
        var json = await _http.GetStringAsync($"http://{encoder.Host}:{encoder.Port}/statistics?json=1", token);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("streams", out var streams))
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
        var json = await _http.GetStringAsync($"http://{encoder.Host}:{encoder.Port}/status-json.xsl", token);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("icestats", out var stats) || !stats.TryGetProperty("source", out var source)) return 0;
        var sources = source.ValueKind == JsonValueKind.Array ? source.EnumerateArray().ToArray() : [source];
        var wanted = NormalizeMount(encoder.Mount);
        foreach (var item in sources)
        {
            var listenUrl = item.TryGetProperty("listenurl", out var u) ? u.GetString() ?? "" : "";
            if ((NormalizeMount(new Uri(listenUrl).AbsolutePath) == wanted || sources.Length == 1) && item.TryGetProperty("listeners", out var listeners)) return listeners.GetInt32();
        }
        return 0;
    }

    private static string NormalizeMount(string? mount) => "/" + (mount ?? "").Trim().TrimStart('/').TrimEnd('/').ToLowerInvariant();
}
