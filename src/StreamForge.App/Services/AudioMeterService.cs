using System.Collections.Concurrent;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using StreamForge.Models;

namespace StreamForge.Services;

public sealed class AudioMeterService : IDisposable
{
    private sealed class MeterState
    {
        public IWaveIn? Capture;
        public AsioOut? Asio;
        public double Left;
        public double Right;
        public bool TestTone;
        public float[] AsioBuffer = [];
        public Timer? TestTimer;
        public WaveStream? Reader;
        public CancellationTokenSource? ReaderStop;
    }

    private readonly ConcurrentDictionary<Guid, MeterState> _states = new();
    public event Action<Guid, byte[], WaveFormat>? PcmAvailable;

    public void Start(SourceInstance instance)
    {
        Stop(instance.Id);
        var state = new MeterState();
        try
        {
            switch (instance.SourceType)
            {
                case "WASAPI": StartWasapi(instance, state); break;
                case "DirectSound": StartWaveIn(instance, state); break;
                case "ASIO": StartAsio(instance, state); break;
                case "File": StartMedia(instance, state, false); break;
                case "Streaming URL": StartMedia(instance, state, true); break;
                case "Test tone": StartTestTone(instance, state); break;
                default: state.Left = state.Right = 0; break;
            }
            _states[instance.Id] = state;
        }
        catch
        {
            state.Capture?.Dispose(); state.Asio?.Dispose();
            _states[instance.Id] = new MeterState();
            throw;
        }
    }

    public (double Left, double Right) Read(Guid instanceId)
    {
        if (!_states.TryGetValue(instanceId, out var state)) return (0, 0);
        if (state.TestTone) return (25.1, 25.1);
        var left = Interlocked.Exchange(ref state.Left, state.Left * 0.72);
        var right = Interlocked.Exchange(ref state.Right, state.Right * 0.72);
        return (Math.Clamp(left * 100, 0, 100), Math.Clamp(right * 100, 0, 100));
    }

    public void Stop(Guid instanceId)
    {
        if (!_states.TryRemove(instanceId, out var state)) return;
        try { state.Capture?.StopRecording(); } catch { }
        try { state.Asio?.Stop(); } catch { }
        state.TestTimer?.Dispose(); state.Capture?.Dispose(); state.Asio?.Dispose();
        state.ReaderStop?.Cancel(); state.Reader?.Dispose(); state.ReaderStop?.Dispose();
    }

    private void StartWasapi(SourceInstance instance, MeterState state)
    {
        using var devices = new MMDeviceEnumerator();
        var device = devices.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).FirstOrDefault(x => x.FriendlyName == instance.Source)
            ?? devices.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
        var capture = new WasapiCapture(device, true, 40);
        capture.DataAvailable += (_, e) => Publish(instance.Id, e.Buffer, e.BytesRecorded, capture.WaveFormat, state);
        capture.StartRecording(); state.Capture = capture;
    }

    private void StartWaveIn(SourceInstance instance, MeterState state)
    {
        var index = Enumerable.Range(0, WaveIn.DeviceCount).FirstOrDefault(i => WaveIn.GetCapabilities(i).ProductName == instance.Source);
        var capture = new WaveInEvent { DeviceNumber = index, WaveFormat = new WaveFormat(instance.InputSampleRate, 16, 2), BufferMilliseconds = 40 };
        capture.DataAvailable += (_, e) => Publish(instance.Id, e.Buffer, e.BytesRecorded, capture.WaveFormat, state);
        capture.StartRecording(); state.Capture = capture;
    }

    private void StartAsio(SourceInstance instance, MeterState state)
    {
        var asio = new AsioOut(instance.Source);
        asio.AudioAvailable += (_, e) =>
        {
            var required = e.SamplesPerBuffer * Math.Max(1, e.InputBuffers.Length);
            if (state.AsioBuffer.Length != required) state.AsioBuffer = new float[required];
            e.GetAsInterleavedSamples(state.AsioBuffer);
            MeasureFloat(state.AsioBuffer, Math.Max(1, e.InputBuffers.Length), state);
            var bytes = new byte[state.AsioBuffer.Length * 4]; Buffer.BlockCopy(state.AsioBuffer, 0, bytes, 0, bytes.Length);
            PcmAvailable?.Invoke(instance.Id, bytes, WaveFormat.CreateIeeeFloatWaveFormat(instance.InputSampleRate, Math.Max(1, e.InputBuffers.Length)));
        };
        asio.InitRecordAndPlayback(null, 2, instance.InputSampleRate);
        asio.Play(); state.Asio = asio;
    }

    private static void MeasurePcm(byte[] buffer, int count, WaveFormat format, MeterState state)
    {
        var channels = Math.Max(1, format.Channels); double left = 0, right = 0;
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            for (var i = 0; i + 3 < count; i += 4) { var sample = Math.Abs(BitConverter.ToSingle(buffer, i)); var channel = (i / 4) % channels; if (channel == 0) left = Math.Max(left, sample); else if (channel == 1) right = Math.Max(right, sample); }
        }
        else if (format.BitsPerSample == 16)
        {
            for (var i = 0; i + 1 < count; i += 2) { var sample = Math.Abs(BitConverter.ToInt16(buffer, i) / 32768d); var channel = (i / 2) % channels; if (channel == 0) left = Math.Max(left, sample); else if (channel == 1) right = Math.Max(right, sample); }
        }
        if (channels == 1) right = left; Interlocked.Exchange(ref state.Left, Math.Max(state.Left, left)); Interlocked.Exchange(ref state.Right, Math.Max(state.Right, right));
    }

    private void Publish(Guid id, byte[] buffer, int count, WaveFormat format, MeterState state)
    {
        MeasurePcm(buffer, count, format, state); var copy = new byte[count]; Buffer.BlockCopy(buffer, 0, copy, 0, count); PcmAvailable?.Invoke(id, copy, format);
    }

    private void StartTestTone(SourceInstance instance, MeterState state)
    {
        state.TestTone = true; state.Left = state.Right = 0.251; var phase = 0d; var format = new WaveFormat(instance.InputSampleRate, 16, 2); var frames = instance.InputSampleRate / 10;
        state.TestTimer = new Timer(_ => { var data = new byte[frames * 4]; for (var i = 0; i < frames; i++) { var sample = (short)(Math.Sin(phase) * 8230); phase += 2 * Math.PI * 1000 / instance.InputSampleRate; if (phase >= 2 * Math.PI) phase -= 2 * Math.PI; data[i * 4] = data[i * 4 + 2] = (byte)sample; data[i * 4 + 1] = data[i * 4 + 3] = (byte)(sample >> 8); } PcmAvailable?.Invoke(instance.Id, data, format); }, null, 0, 100);
    }

    private void StartMedia(SourceInstance instance, MeterState state, bool isUrl)
    {
        if (string.IsNullOrWhiteSpace(instance.Source)) throw new InvalidOperationException(isUrl ? "Inserire l'URL dello stream." : "Selezionare un file audio.");
        WaveStream reader = isUrl ? new MediaFoundationReader(instance.Source) : new AudioFileReader(instance.Source);
        state.Reader = reader; state.ReaderStop = new CancellationTokenSource(); var token = state.ReaderStop.Token;
        _ = Task.Run(async () =>
        {
            WaveStream? current = reader;
            while (!token.IsCancellationRequested)
            {
                if (current is null) { try { current = new MediaFoundationReader(instance.Source); state.Reader = current; } catch { await Task.Delay(TimeSpan.FromSeconds(5), token); continue; } }
                try
                {
                    var buffer = new byte[Math.Max(4096, current.WaveFormat.AverageBytesPerSecond / 10)];
                    while (!token.IsCancellationRequested)
                    {
                        var read = current.Read(buffer, 0, buffer.Length);
                        if (read == 0) { if (!isUrl && current.CanSeek) { current.Position = 0; continue; } break; }
                        Publish(instance.Id, buffer, read, current.WaveFormat, state);
                        await Task.Delay(Math.Max(1, (int)(read * 1000d / current.WaveFormat.AverageBytesPerSecond)), token);
                    }
                    if (!isUrl) break;
                }
                catch (OperationCanceledException) { break; }
                catch { if (!isUrl) break; }
                try { current.Dispose(); } catch { }
                if (!isUrl || token.IsCancellationRequested) break;
                try { await Task.Delay(TimeSpan.FromSeconds(5), token); current = new MediaFoundationReader(instance.Source); state.Reader = current; } catch (OperationCanceledException) { break; } catch { current = null; }
            }
        }, token);
    }

    private static void MeasureFloat(float[] samples, int channels, MeterState state)
    {
        double left = 0, right = 0; for (var i = 0; i < samples.Length; i++) { var sample = Math.Abs(samples[i]); var channel = i % channels; if (channel == 0) left = Math.Max(left, sample); else if (channel == 1) right = Math.Max(right, sample); } if (channels == 1) right = left; Interlocked.Exchange(ref state.Left, Math.Max(state.Left, left)); Interlocked.Exchange(ref state.Right, Math.Max(state.Right, right));
    }

    public void Dispose() { foreach (var id in _states.Keys) Stop(id); }
}
