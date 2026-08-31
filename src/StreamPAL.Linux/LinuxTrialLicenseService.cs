using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StreamPAL.Linux;

public sealed class LinuxTrialLicenseService : IDisposable
{
#if TRIAL
    public const bool IsTrialBuild = true;
#else
    public const bool IsTrialBuild = false;
#endif
    private const double LimitSeconds = 3600;
    private const string Endpoint = "https://www.freewaves.it/Streampal_license/activate.php";
    private const string PublicKey = """
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEbUv366DtwI0lTeQYrfsX9oXA98r8
dYgFlhvABI43NE/6WM4mC7qowj/EWWuAu/ya17Et+fNWhOAQU/+BE9wmAA==
-----END PUBLIC KEY-----
""";
    private readonly string _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StreamPAL");
    private readonly Stopwatch _watch = Stopwatch.StartNew(); private State _state; private readonly byte[] _stateKey;
    public LinuxTrialLicenseService(){Directory.CreateDirectory(_folder);InstallationCode=BuildId();_stateKey=SHA256.HashData(Encoding.UTF8.GetBytes("StreamPAL-State|"+InstallationCode));_state=Load();IsRegistered=Validate(ReadLicense());}
    public string InstallationCode{get;} public bool IsRegistered{get;private set;} public bool IsExpired=>IsTrialBuild&&!IsRegistered&&Remaining<=TimeSpan.Zero;public bool NoticeShown=>_state.NoticeShown;public TimeSpan Remaining=>IsRegistered||!IsTrialBuild?TimeSpan.MaxValue:TimeSpan.FromSeconds(Math.Max(0,LimitSeconds-_state.UsedSeconds-_watch.Elapsed.TotalSeconds));
    public void MarkNoticeShown(){_state.NoticeShown=true;Checkpoint();}
    public void Checkpoint(){if(!IsTrialBuild||IsRegistered)return;_state.UsedSeconds=Math.Min(LimitSeconds,_state.UsedSeconds+_watch.Elapsed.TotalSeconds);_watch.Restart();Save();}
    public async Task<(bool Success,string Error)> TryRegisterAsync(string code){code=code.Trim();if(!Validate(code))return(false,"Il codice non è valido per questo computer.");try{using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(12)};using var response=await client.PostAsJsonAsync(Endpoint,new{code,installationCode=InstallationCode});var result=await response.Content.ReadFromJsonAsync<Response>();if(!response.IsSuccessStatusCode||result?.Valid!=true)return(false,result?.Message=="Key already used on another computer"?"Chiave non valida: già usata su un altro computer.":"Chiave non valida.");}catch{return(false,"Impossibile contattare il server di attivazione.");}await File.WriteAllTextAsync(Path.Combine(_folder,"license.key"),code);IsRegistered=true;return(true,"");}
    private bool Validate(string code){if(!IsTrialBuild)return true;try{var p=code.Split('.');if(p.Length!=3||p[0]!="SP1")return false;var payload=B64(p[1]);var signature=B64(p[2]);if(Encoding.UTF8.GetString(payload)!=$"StreamPAL|{InstallationCode}")return false;using var e=ECDsa.Create();e.ImportFromPem(PublicKey);return e.VerifyData(payload,signature,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);}catch{return false;}}
    private string ReadLicense(){try{return File.ReadAllText(Path.Combine(_folder,"license.key")).Trim();}catch{return"";}}
    private State Load(){try{var envelope=JsonSerializer.Deserialize<Envelope>(File.ReadAllText(Path.Combine(_folder,"trial-linux.json")))!;var data=Convert.FromBase64String(envelope.Data);using var h=new HMACSHA256(_stateKey);if(!CryptographicOperations.FixedTimeEquals(h.ComputeHash(data),Convert.FromBase64String(envelope.Mac)))return new();return JsonSerializer.Deserialize<State>(data)??new();}catch{return new();}}
    private void Save(){try{var data=JsonSerializer.SerializeToUtf8Bytes(_state);using var h=new HMACSHA256(_stateKey);File.WriteAllText(Path.Combine(_folder,"trial-linux.json"),JsonSerializer.Serialize(new Envelope{Data=Convert.ToBase64String(data),Mac=Convert.ToBase64String(h.ComputeHash(data))}));}catch{}}
    private static string BuildId(){string machine;try{machine=File.ReadAllText("/etc/machine-id").Trim();}catch{machine=Environment.MachineName;}var hash=SHA256.HashData(Encoding.UTF8.GetBytes("StreamPAL|"+machine));return string.Join('-',Convert.ToHexString(hash[..12]).Chunk(4).Select(x=>new string(x)));}
    private static byte[] B64(string value){value=value.Replace('-','+').Replace('_','/');value+=new string('=',(4-value.Length%4)%4);return Convert.FromBase64String(value);}
    public void Dispose(){Checkpoint();_watch.Stop();}
    private sealed class State{public double UsedSeconds{get;set;}public bool NoticeShown{get;set;}}private sealed class Envelope{public string Data{get;set;}="";public string Mac{get;set;}="";}private sealed class Response{public bool Valid{get;set;}public string Message{get;set;}="";}
}
