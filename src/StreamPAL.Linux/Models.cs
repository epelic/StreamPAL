using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
namespace StreamPAL.Linux;
public sealed class EncoderProfile : INotifyPropertyChanged
{
    private bool _connected; private int _listeners; private string _log = "";
    public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = "Stream 1"; public string ChannelMode { get; set; } = "Stereo"; public string Codec { get; set; } = "MP3"; public int BitrateKbps { get; set; } = 128; public int SampleRate { get; set; } = 44100;
    public string ServerType { get; set; } = "Icecast 2"; public string Host { get; set; } = "localhost"; public int Port { get; set; } = 8000; public string Mount { get; set; } = "/stream"; public string Password { get; set; } = ""; public string Metadata { get; set; } = ""; public string StationName { get; set; } = ""; public string Description { get; set; } = ""; public string StationUrl { get; set; } = ""; public string Genre { get; set; } = ""; public bool Enabled { get; set; }
    [JsonIgnore] public bool IsRunning { get; set; } [JsonIgnore] public bool IsConnected { get => _connected; set { _connected = value; Changed(); } } [JsonIgnore] public int Listeners { get => _listeners; set { _listeners = value; Changed(); } } [JsonIgnore] public string Log { get => _log; set { _log = value; Changed(); } }
    public void AddLog(string s) => Log += (Log.Length > 0 ? "\n" : "") + $"[{DateTime.Now:HH:mm:ss}] {s}";
    public override string ToString() => $"{Name} · {Codec} · {(IsConnected ? "connesso" : "off")} · {Listeners}";
    public event PropertyChangedEventHandler? PropertyChanged; private void Changed([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
}
public sealed class SourceInstance
{
    public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = "Istanza 1"; public string SourceType { get; set; } = "PipeWire"; public string Source { get; set; } = "@DEFAULT_AUDIO_SOURCE@"; public int InputSampleRate { get; set; } = 48000; public List<EncoderProfile> Encoders { get; set; } = [new()];
    public override string ToString() => $"{Name} · {SourceType}";
}
