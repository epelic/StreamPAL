using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StreamForge.Models;

public sealed class EncoderProfile : INotifyPropertyChanged
{
    private bool _isConnected;
    private int _listeners;
    private string _connectionLog = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nuovo encoder";
    public string ChannelMode { get; set; } = "Stereo";
    public string Codec { get; set; } = "MP3";
    public int BitrateKbps { get; set; } = 128;
    public int SampleRate { get; set; } = 44100;
    public string ServerType { get; set; } = "Icecast 2";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8000;
    public string Mount { get; set; } = "/stream";
    public string Username { get; set; } = "source";
    public string Password { get; set; } = "";
    public string Metadata { get; set; } = "";
    public string StationName { get; set; } = "";
    public string Description { get; set; } = "";
    public string StationUrl { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Aim { get; set; } = "";
    public string Irc { get; set; } = "";
    public string Icq { get; set; } = "";
    public bool Enabled { get; set; }
    [JsonIgnore] public bool IsRunning { get; set; }
    [JsonIgnore] public string Status => IsRunning ? "attivo" : "off";
    [JsonIgnore] public string Summary => $"{Codec} {BitrateKbps}k · {Host}:{Port}{Mount}";
    [JsonIgnore] public bool IsConnected { get => _isConnected; set { if (_isConnected == value) return; _isConnected = value; Changed(); Changed(nameof(ConnectionColor)); Changed(nameof(Status)); } }
    [JsonIgnore] public string ConnectionColor => IsConnected ? "#39DC79" : "#F05252";
    [JsonIgnore] public int Listeners { get => _listeners; set { if (_listeners == value) return; _listeners = value; Changed(); } }
    [JsonIgnore] public string ConnectionLog { get => _connectionLog; private set { _connectionLog = value; Changed(); } }
    [JsonIgnore] public double LeftLevel { get; set; }
    [JsonIgnore] public double RightLevel { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void AddLog(string message) { var line = $"[{DateTime.Now:HH:mm:ss}] {message}"; ConnectionLog = string.IsNullOrEmpty(ConnectionLog) ? line : $"{ConnectionLog}\n{line}"; var lines = ConnectionLog.Split('\n'); if (lines.Length > 80) ConnectionLog = string.Join('\n', lines[^80..]); }
    public void ClearLog() => ConnectionLog = "";
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
