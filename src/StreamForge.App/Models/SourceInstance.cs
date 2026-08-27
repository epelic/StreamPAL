using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StreamForge.Models;
public sealed class SourceInstance : INotifyPropertyChanged
{
    private double _leftLevel, _rightLevel;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Istanza 1";
    public string SourceType { get; set; } = "Test tone";
    public string Source { get; set; } = "Generatore interno";
    public int InputSampleRate { get; set; } = 48000;
    public List<EncoderProfile> Encoders { get; set; } = [];
    [JsonIgnore] public double LeftLevel { get => _leftLevel; set { _leftLevel = value; Changed(); Changed(nameof(LeftDb)); Changed(nameof(VuDb)); } }
    [JsonIgnore] public double RightLevel { get => _rightLevel; set { _rightLevel = value; Changed(); Changed(nameof(RightDb)); Changed(nameof(VuDb)); } }
    [JsonIgnore] public string LeftDb => Db(LeftLevel);
    [JsonIgnore] public string RightDb => Db(RightLevel);
    [JsonIgnore] public string VuDb => $"{LeftDb}/{RightDb} dB";
    [JsonIgnore] public int TotalListeners => Encoders.Sum(x => x.Listeners);
    public event PropertyChangedEventHandler? PropertyChanged;
    public void RefreshStats() => Changed(nameof(TotalListeners));
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private static string Db(double level) => level <= 0 ? "−∞" : $"{20 * Math.Log10(level / 100):0.0}";
}
