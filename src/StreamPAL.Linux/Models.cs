using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
namespace StreamPAL.Linux;
public sealed class EncoderProfile : INotifyPropertyChanged
{
    private bool _connected; private int _listeners; private string _log = "";
    public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = "Stream 1"; public string ChannelMode { get; set; } = "Stereo"; public string OutputMode { get; set; } = "Stereo"; public string Codec { get; set; } = "MP3"; public int BitrateKbps { get; set; } = 128; public int SampleRate { get; set; } = 44100;
    public string ServerType { get; set; } = "Icecast 2"; public string Host { get; set; } = "localhost"; public int Port { get; set; } = 8000; public string Mount { get; set; } = "/stream"; public string Password { get; set; } = ""; public string Metadata { get; set; } = ""; public string StationName { get; set; } = ""; public string Description { get; set; } = ""; public string StationUrl { get; set; } = ""; public string Genre { get; set; } = ""; public bool Enabled { get; set; }
    [JsonIgnore] public bool IsRunning { get; set; } [JsonIgnore] public bool IsConnected { get => _connected; set { _connected = value; Changed(); Changed(nameof(DisplayText)); } } [JsonIgnore] public int Listeners { get => _listeners; set { _listeners = value; Changed(); Changed(nameof(DisplayText)); } } [JsonIgnore] public string Log { get => _log; set { _log = value; Changed(); } }
    public void AddLog(string s) => Log += (Log.Length > 0 ? "\n" : "") + $"[{DateTime.Now:HH:mm:ss}] {s}";
    [JsonIgnore] public string DisplayText => $"{Name} · {Codec} · {(IsConnected ? "connesso" : "off")} · {Listeners}";
    public override string ToString() => DisplayText;
    public event PropertyChangedEventHandler? PropertyChanged; private void Changed([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
}
public sealed class SourceInstance : INotifyPropertyChanged
{
    private double _leftLevel, _rightLevel;
    public Guid Id { get; set; } = Guid.NewGuid(); public string Name { get; set; } = "Istanza 1"; public string SourceType { get; set; } = "PipeWire"; public string Source { get; set; } = "@DEFAULT_AUDIO_SOURCE@"; public int InputSampleRate { get; set; } = 48000; public List<EncoderProfile> Encoders { get; set; } = [new()];
    [JsonIgnore] public double LeftLevel { get => _leftLevel; set { _leftLevel = value; Changed(); Changed(nameof(VuDb)); } }
    [JsonIgnore] public double RightLevel { get => _rightLevel; set { _rightLevel = value; Changed(); Changed(nameof(VuDb)); } }
    [JsonIgnore] public int TotalListeners => Encoders.Sum(x => x.Listeners);
    [JsonIgnore] public string DisplayText => $"{Name} · {SourceType} · 👥 {TotalListeners}";
    [JsonIgnore] public string VuDb => $"L {Db(LeftLevel)} / R {Db(RightLevel)} dB";
    private static string Db(double level) => level <= 0.001 ? "−∞" : $"{20 * Math.Log10(level / 100):0.0}";
    public void NotifyListenersChanged(){Changed(nameof(TotalListeners));Changed(nameof(DisplayText));}
    public override string ToString() => DisplayText;
    public event PropertyChangedEventHandler? PropertyChanged; private void Changed([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
}

public sealed class ListenerSample
{
    public DateTime TimestampUtc { get; set; }
    public Dictionary<Guid, int> Streams { get; set; } = [];
    [JsonIgnore] public int Total => Streams.Values.Sum();
}
