using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace StreamForge.Services;
public sealed class RoutingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source; private readonly string _mode; private readonly int _inputChannels; private float[] _inputBuffer = [];
    public WaveFormat WaveFormat { get; }
    public RoutingSampleProvider(ISampleProvider source, string mode)
    {
        _source = source; _mode = mode; _inputChannels = source.WaveFormat.Channels;
        var outputChannels = mode == "Mono (L+R)" ? 1 : 2;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, outputChannels);
    }
    public int Read(float[] buffer, int offset, int count)
    {
        var outputChannels = WaveFormat.Channels; var frames = count / outputChannels; var required = frames * _inputChannels; if (_inputBuffer.Length < required) _inputBuffer = new float[required]; var read = _source.Read(_inputBuffer, 0, required); var inputFrames = read / _inputChannels;
        for (var f = 0; f < inputFrames; f++) { var l = _inputBuffer[f * _inputChannels]; var r = _inputChannels > 1 ? _inputBuffer[f * _inputChannels + 1] : l; if (outputChannels == 1) buffer[offset + f] = (l + r) * .5f; else { var selected = _mode == "Solo sinistro" ? l : _mode == "Solo destro" ? r : 0; buffer[offset + f * 2] = _mode.StartsWith("Solo") ? selected : l; buffer[offset + f * 2 + 1] = _mode.StartsWith("Solo") ? selected : r; } }
        return inputFrames * outputChannels;
    }
}
