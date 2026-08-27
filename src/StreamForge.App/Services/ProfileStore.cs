using System.IO;
using System.Text.Json;
using StreamForge.Models;

namespace StreamForge.Services;

public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamPAL", "profiles.json");

    public async Task<List<SourceInstance>> LoadAsync()
    {
        if (!File.Exists(_path)) return [];
        await using var stream = File.OpenRead(_path);
        var instances = await JsonSerializer.DeserializeAsync<List<SourceInstance>>(stream, Options) ?? [];
        foreach (var encoder in instances.SelectMany(x => x.Encoders))
        {
            if (encoder.StationName == "Local Flac") encoder.StationName = "";
            if (encoder.Genre == "Other") encoder.Genre = "";
            if (encoder.Metadata == "StreamPAL") encoder.Metadata = "";
        }
        return instances;
    }

    public async Task SaveAsync(IEnumerable<SourceInstance> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await ExportAsync(_path, profiles);
    }

    public async Task ExportAsync(string path, IEnumerable<SourceInstance> profiles)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, profiles, Options);
    }

    public async Task<List<SourceInstance>> ImportAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<SourceInstance>>(stream, Options) ?? [];
    }
}
