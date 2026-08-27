using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using StreamForge.Models;

namespace StreamForge.Services;

public sealed class StatisticsService
{
    public static StatisticsService Instance { get; } = new();
    private readonly ConcurrentDictionary<Guid, List<ListenerSample>> _history = new();
    private readonly string _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamPAL", "statistics");
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public IReadOnlyList<ListenerSample> Get(SourceInstance instance)
    {
        var list = _history.GetOrAdd(instance.Id, Load);
        lock (list) return list.ToList();
    }

    public async Task RecordAsync(SourceInstance instance)
    {
        var list = _history.GetOrAdd(instance.Id, Load);
        var cutoff = DateTime.UtcNow.AddHours(-72);
        lock (list)
        {
            list.RemoveAll(x => x.TimestampUtc < cutoff);
            list.Add(new ListenerSample { TimestampUtc = DateTime.UtcNow, Streams = instance.Encoders.Where(x => x.Enabled || x.IsRunning).ToDictionary(x => x.Id, x => x.Listeners) });
        }
        Directory.CreateDirectory(_folder);
        List<ListenerSample> snapshot;
        lock (list) snapshot = list.ToList();
        await File.WriteAllTextAsync(FilePath(instance.Id), JsonSerializer.Serialize(snapshot, _json));
    }

    private List<ListenerSample> Load(Guid id)
    {
        try
        {
            var path = FilePath(id);
            if (!File.Exists(path)) return [];
            var items = JsonSerializer.Deserialize<List<ListenerSample>>(File.ReadAllText(path), _json) ?? [];
            items.RemoveAll(x => x.TimestampUtc < DateTime.UtcNow.AddHours(-72));
            return items;
        }
        catch { return []; }
    }

    private string FilePath(Guid id) => Path.Combine(_folder, $"{id:N}.json");
}
