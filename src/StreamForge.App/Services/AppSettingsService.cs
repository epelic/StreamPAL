using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using StreamForge.Models;

namespace StreamForge.Services;
public sealed class AppSettingsService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamPAL", "settings.json");
    public AppSettings Load() { try { if (File.Exists(_path)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new(); using var key = Registry.CurrentUser.OpenSubKey(@"Software\FreeWaves\StreamPAL"); var installer = key?.GetValue("InstallerLanguage")?.ToString(); return new AppSettings { Language = installer switch { "english" => "en", "spanish" => "es", "french" => "fr", _ => "it" } }; } catch { return new(); } }
    public void Save(AppSettings settings) { Directory.CreateDirectory(Path.GetDirectoryName(_path)!); File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true })); ApplyStartup(settings); }
    private static void ApplyStartup(AppSettings settings)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (!settings.StartWithWindows && !settings.StartAllWithWindows) { key.DeleteValue("StreamPAL", false); return; }
        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "StreamPAL.exe";
        var mode = settings.StartAllWithWindows ? " --start-all" : " --autostart";
        key.SetValue("StreamPAL", $"\"{exe}\"{mode}", RegistryValueKind.String);
    }
}
