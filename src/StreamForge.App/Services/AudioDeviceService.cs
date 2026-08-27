using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace StreamForge.Services;

public sealed record AudioDevice(string Id, string Name);

public sealed class AudioDeviceService
{
    public IReadOnlyList<AudioDevice> Enumerate(string backend)
    {
        try
        {
            return backend switch
            {
                "WASAPI" => Wasapi(),
                "ASIO" => AsioOut.GetDriverNames().Select(x => new AudioDevice(x, x)).ToList(),
                "DirectSound" => Enumerable.Range(0, WaveIn.DeviceCount).Select(i => new AudioDevice(i.ToString(), WaveIn.GetCapabilities(i).ProductName)).ToList(),
                _ => []
            };
        }
        catch (Exception ex)
        {
            return [new AudioDevice("error", $"Errore rilevamento: {ex.Message}")];
        }
    }

    private static IReadOnlyList<AudioDevice> Wasapi()
    {
        using var devices = new MMDeviceEnumerator();
        return devices.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(x => new AudioDevice(x.ID, x.FriendlyName)).ToList();
    }
}
