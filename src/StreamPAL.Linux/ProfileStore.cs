using System.Text.Json;
namespace StreamPAL.Linux;
public sealed class ProfileStore
{
    private readonly string _folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"StreamPAL");private string FilePath=>Path.Combine(_folder,"profiles.json");private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public async Task<List<SourceInstance>> LoadAsync(){try{return File.Exists(FilePath)?JsonSerializer.Deserialize<List<SourceInstance>>(await File.ReadAllTextAsync(FilePath),Json)??[]:[];}catch{return[];}}
    public async Task SaveAsync(IEnumerable<SourceInstance> data){Directory.CreateDirectory(_folder);await File.WriteAllTextAsync(FilePath,JsonSerializer.Serialize(data,Json));}
    public async Task ExportAsync(string path,IEnumerable<SourceInstance> data)=>await File.WriteAllTextAsync(path,JsonSerializer.Serialize(data,Json));
    public async Task<List<SourceInstance>> ImportAsync(string path)=>JsonSerializer.Deserialize<List<SourceInstance>>(await File.ReadAllTextAsync(path),Json)??[];
}
