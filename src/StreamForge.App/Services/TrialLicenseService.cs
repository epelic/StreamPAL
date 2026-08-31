using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Win32;

namespace StreamForge.Services;

public sealed class TrialLicenseService : IDisposable
{
#if TRIAL
    public const bool IsTrialBuild = true;
#else
    public const bool IsTrialBuild = false;
#endif
    private const double TrialSeconds = 60 * 60;
    private const string ActivationEndpoint = "https://www.freewaves.it/Streampal_license/activate.php";
    private const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEbUv366DtwI0lTeQYrfsX9oXA98r8
dYgFlhvABI43NE/6WM4mC7qowj/EWWuAu/ya17Et+fNWhOAQU/+BE9wmAA==
-----END PUBLIC KEY-----
""";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("StreamPAL-Trial-State-v1");
    private readonly string _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamPAL");
    private readonly Stopwatch _session = Stopwatch.StartNew();
    private TrialState _state;

    public TrialLicenseService()
    {
        Directory.CreateDirectory(_folder);
        InstallationCode = BuildInstallationCode();
        _state = LoadState();
        IsRegistered = ValidateLicense(ReadLicense());
    }

    public string InstallationCode { get; }
    public bool IsRegistered { get; private set; }
    public bool IsExpired => IsTrialBuild && !IsRegistered && Remaining <= TimeSpan.Zero;
    public bool NoticeShown => _state.NoticeShown;
    public TimeSpan Remaining => IsRegistered || !IsTrialBuild ? TimeSpan.MaxValue : TimeSpan.FromSeconds(Math.Max(0, TrialSeconds - _state.UsedSeconds - _session.Elapsed.TotalSeconds));

    public void MarkNoticeShown() { _state.NoticeShown = true; Checkpoint(); }

    public void Checkpoint()
    {
        if (!IsTrialBuild || IsRegistered) return;
        _state.UsedSeconds = Math.Min(TrialSeconds, _state.UsedSeconds + _session.Elapsed.TotalSeconds);
        _state.LastSavedUtc = DateTime.UtcNow;
        _session.Restart();
        SaveState();
    }

    public async Task<(bool Success, string Error)> TryRegisterAsync(string code)
    {
        code = code.Trim();
        if (!ValidateLicense(code)) return (false, "Il codice non è valido per questo PC.");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            using var response = await client.PostAsJsonAsync(ActivationEndpoint, new { code, installationCode = InstallationCode });
            var result = await response.Content.ReadFromJsonAsync<ActivationResponse>();
            if (!response.IsSuccessStatusCode || result?.Valid != true) return (false, result?.Message == "Key already used on another computer" ? "Chiave non valida: è già stata usata su un altro computer." : "Chiave non valida.");
        }
        catch { return (false, "Impossibile contattare il server di attivazione. Controlla la connessione Internet e riprova."); }
        File.WriteAllText(Path.Combine(_folder, "license.key"), code, Encoding.UTF8); IsRegistered = true; return (true, "");
    }

    private bool ValidateLicense(string code)
    {
        if (!IsTrialBuild || string.IsNullOrWhiteSpace(code)) return !IsTrialBuild;
        try
        {
            var parts = code.Split('.');
            if (parts.Length != 3 || parts[0] != "SP1") return false;
            var payload = FromBase64Url(parts[1]);
            var signature = FromBase64Url(parts[2]);
            if (Encoding.UTF8.GetString(payload) != $"StreamPAL|{InstallationCode}") return false;
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(PublicKeyPem);
            return verifier.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch { return false; }
    }

    private string ReadLicense()
    {
        try { return File.ReadAllText(Path.Combine(_folder, "license.key"), Encoding.UTF8).Trim(); }
        catch { return ""; }
    }

    private TrialState LoadState()
    {
        try
        {
            var encrypted = File.ReadAllBytes(Path.Combine(_folder, "trial.dat"));
            var json = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<TrialState>(json) ?? new();
        }
        catch { return new(); }
    }

    private void SaveState()
    {
        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(_state);
            var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(Path.Combine(_folder, "trial.dat"), encrypted);
        }
        catch { }
    }

    private static string BuildInstallationCode()
    {
        string machine;
        try { machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")?.GetValue("MachineGuid")?.ToString() ?? Environment.MachineName; }
        catch { machine = Environment.MachineName; }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("StreamPAL|" + machine));
        return string.Join('-', Convert.ToHexString(hash[..12]).Chunk(4).Select(x => new string(x)));
    }

    private static byte[] FromBase64Url(string value)
    {
        value = value.Replace('-', '+').Replace('_', '/');
        value += new string('=', (4 - value.Length % 4) % 4);
        return Convert.FromBase64String(value);
    }

    public void Dispose() { Checkpoint(); _session.Stop(); }
    private sealed class TrialState { public double UsedSeconds { get; set; } public DateTime LastSavedUtc { get; set; } public bool NoticeShown { get; set; } }
    private sealed class ActivationResponse { public bool Valid { get; set; } public string Message { get; set; } = ""; }
}
