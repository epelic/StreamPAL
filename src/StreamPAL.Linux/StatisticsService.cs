using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace StreamPAL.Linux;

public sealed class StatisticsService
{
    public static StatisticsService Instance { get; } = new();
    private readonly ConcurrentDictionary<Guid,List<ListenerSample>> _history = new();
    private readonly string _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamPAL", "statistics");

    public IReadOnlyList<ListenerSample> Get(SourceInstance instance){var list=_history.GetOrAdd(instance.Id,Load);lock(list)return list.ToList();}
    public async Task RecordAsync(SourceInstance instance)
    {
        var list=_history.GetOrAdd(instance.Id,Load);lock(list){list.RemoveAll(x=>x.TimestampUtc<DateTime.UtcNow.AddHours(-72));list.Add(new(){TimestampUtc=DateTime.UtcNow,Streams=instance.Encoders.Where(x=>x.Enabled||x.IsRunning).ToDictionary(x=>x.Id,x=>x.Listeners)});}Directory.CreateDirectory(_folder);List<ListenerSample> copy;lock(list)copy=list.ToList();await File.WriteAllTextAsync(PathFor(instance.Id),JsonSerializer.Serialize(copy));
    }
    public void ExportCsv(string path,SourceInstance instance)
    {
        var samples=Get(instance);var encoders=instance.Encoders.Where(e=>samples.Any(s=>s.Streams.ContainsKey(e.Id))).ToList();var csv=new StringBuilder("timestamp");foreach(var e in encoders)csv.Append(',').Append(Escape(e.Name));csv.AppendLine(",totale");foreach(var s in samples){csv.Append(s.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));foreach(var e in encoders)csv.Append(',').Append(s.Streams.GetValueOrDefault(e.Id));csv.Append(',').Append(s.Total).AppendLine();}File.WriteAllText(path,csv.ToString(),new UTF8Encoding(true));
    }
    private List<ListenerSample> Load(Guid id){try{var p=PathFor(id);if(!File.Exists(p))return[];var x=JsonSerializer.Deserialize<List<ListenerSample>>(File.ReadAllText(p))??[];x.RemoveAll(s=>s.TimestampUtc<DateTime.UtcNow.AddHours(-72));return x;}catch{return[];}}
    private string PathFor(Guid id)=>Path.Combine(_folder,$"{id:N}.json");
    private static string Escape(string value)=>value.Contains(',')||value.Contains('"')?$"\"{value.Replace("\"","\"\"")}\"":value;
}
