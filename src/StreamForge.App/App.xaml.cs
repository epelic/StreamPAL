using System.IO;
using System.Net.Http;
using System.Windows;
using StreamForge.Models;
using StreamForge.Services;
namespace StreamForge;
public partial class App : Application
{
    public App()
    {
        Startup += (_, e) =>
        {
            if (e.Args.Contains("--trial-id"))
            {
                using var license = new TrialLicenseService(); File.WriteAllText(Path.Combine(Path.GetTempPath(), "StreamPAL-trial-id.txt"), license.InstallationCode); Shutdown(0); return;
            }
            if (e.Args.Contains("--trial-activate-test"))
            {
                Dispatcher.BeginInvoke(async () => { using var license = new TrialLicenseService(); var code = Environment.GetEnvironmentVariable("STREAMPAL_TEST_LICENSE") ?? ""; var result = await license.TryRegisterAsync(code); File.WriteAllText(Path.Combine(Path.GetTempPath(), "StreamPAL-trial-activation.txt"), $"success={result.Success}\nerror={result.Error}"); Shutdown(result.Success ? 0 : 6); }); return;
            }
            if (e.Args.Contains("--aac-self-test"))
            {
                Dispatcher.BeginInvoke(async () =>
                {
                    var report = new List<string>();
                    try
                    {
                        foreach (var channels in new[] { 1, 2 }) foreach (var bitrate in new[] { 16000, 24000, 48000 })
                        {
                            using var encoder = new FdkAacEncoder(44100, channels, bitrate, true);
                            var pcm = new short[encoder.InputSamplesPerFrame];
                            for (var i = 0; i < pcm.Length; i++) pcm[i] = (short)(Math.Sin(i * 2 * Math.PI * 700 / 44100) * 10000);
                            var total = 0; var frames = 0;
                            for (var n = 0; n < 30; n++) { var encoded = encoder.Encode(pcm); if (encoded.Length > 0) { if (encoded.Length < 7 || encoded[0] != 0xFF || (encoded[1] & 0xF0) != 0xF0) throw new InvalidDataException("Frame ADTS non valido"); total += encoded.Length; frames++; } }
                            if (frames < 25 || total <= 0) throw new InvalidDataException($"AAC+ {channels}ch {bitrate}bps non produce frame sufficienti");
                            report.Add($"AAC+ {channels}ch {bitrate / 1000}kbps: {frames} frame, {total} byte OK");
                        }
                        await File.WriteAllLinesAsync(Path.Combine(Path.GetTempPath(), "StreamPAL-aac-self-test.txt"), report); Shutdown(0);
                    }
                    catch (Exception ex) { report.Add(ex.ToString()); await File.WriteAllLinesAsync(Path.Combine(Path.GetTempPath(), "StreamPAL-aac-self-test.txt"), report); Shutdown(5); }
                });
                return;
            }
            if (e.Args.Contains("--stress-test"))
            {
                Dispatcher.BeginInvoke(async () => { try { var result = await StressTestService.RunAsync(); await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "StreamPAL-stress-test.txt"), $"instances={result.Instances}\nencoders={result.Encoders}\nbytes={result.PcmBytesProcessed}\nelapsed={result.Elapsed.TotalSeconds:F3}\nmemory={result.MemoryDelta}"); Shutdown(result.Encoders == 256 && result.PcmBytesProcessed > 0 ? 0 : 4); } catch (Exception ex) { await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), "StreamPAL-stress-test.txt"), ex.ToString()); Shutdown(2); } });
                return;
            }
            if (e.Args.Contains("--stream-smoke"))
            {
                Dispatcher.BeginInvoke(async () =>
                {
                    var logFile = Path.Combine(Path.GetTempPath(), "StreamPAL-stream-smoke.txt");
                    try
                    {
                        var host = Environment.GetEnvironmentVariable("STREAMPAL_TEST_HOST") ?? throw new InvalidDataException("Host test mancante");
                        var password = Environment.GetEnvironmentVariable("STREAMPAL_TEST_PASSWORD") ?? throw new InvalidDataException("Password test mancante");
                        var port = int.Parse(Environment.GetEnvironmentVariable("STREAMPAL_TEST_PORT") ?? "8070");
                        var codec = Environment.GetEnvironmentVariable("STREAMPAL_TEST_CODEC") ?? "MP3";
                        var serverType = Environment.GetEnvironmentVariable("STREAMPAL_TEST_SERVER") ?? "SHOUTcast v2";
                        var mount = Environment.GetEnvironmentVariable("STREAMPAL_TEST_MOUNT") ?? "/stream";
                        var metadata = Environment.GetEnvironmentVariable("STREAMPAL_TEST_METADATA") ?? "";
                        var encoder = new EncoderProfile { Host = host, Port = port, Password = password, ServerType = serverType, Mount = mount, Codec = codec, BitrateKbps = 128, SampleRate = 44100, StationName = "StreamPAL test", Metadata = metadata };
                        using IBroadcastSession session = codec switch { "AAC-LC" => new AacBroadcastSession(encoder, new NAudio.Wave.WaveFormat(48000, 16, 2), false), "AAC+ (HE-AAC)" => new AacBroadcastSession(encoder, new NAudio.Wave.WaveFormat(48000, 16, 2), true), "Opus" => new OpusBroadcastSession(encoder, new NAudio.Wave.WaveFormat(48000, 16, 2)), "OGG Vorbis" => new OggBroadcastSession(encoder, new NAudio.Wave.WaveFormat(48000, 16, 2)), _ => new Mp3BroadcastSession(encoder, new NAudio.Wave.WaveFormat(48000, 16, 2)) };
                        using var listenerStop = new CancellationTokenSource(); Task? listener = null;
                        var phase = 0d;
                        for (var cycle = 0; cycle < 150; cycle++)
                        {
                            var data = new byte[4800 * 4];
                            for (var i = 0; i < 4800; i++) { var sample = (short)(Math.Sin(phase) * 4096); phase += 2 * Math.PI * 1000 / 48000; data[i * 4] = data[i * 4 + 2] = (byte)sample; data[i * 4 + 1] = data[i * 4 + 3] = (byte)(sample >> 8); }
                            session.Feed(data); if (cycle == 50) listener = ListenForSmokeAsync($"http://{host}:{port}{(mount.StartsWith('/') ? mount : "/" + mount)}", listenerStop.Token); if (cycle == 80 && !string.IsNullOrEmpty(metadata)) { await new MetadataUpdateService().UpdateAsync(encoder); encoder.AddLog("Metadata test aggiornati"); } await Task.Delay(100);
                        }
                        for (var retry = 0; retry < 5; retry++) { encoder.Listeners = await new ListenerMonitorService().ReadAsync(encoder); if (encoder.Listeners > 0) break; await Task.Delay(500); }
                        encoder.AddLog($"Ascoltatori rilevati: {encoder.Listeners}");
                        listenerStop.Cancel(); if (listener is not null) try { await listener; } catch { }
                        await File.WriteAllTextAsync(logFile, encoder.ConnectionLog);
                        Shutdown(encoder.IsConnected ? 0 : 3);
                    }
                    catch (Exception ex) { await File.WriteAllTextAsync(logFile, ex.ToString()); Shutdown(2); }
                });
                return;
            }
            if (!e.Args.Contains("--smoke-test")) return;
            Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    var instance = new SourceInstance { Name = "Smoke test", Encoders = [new EncoderProfile { Name = "Test", Enabled = true, Listeners = 3 }] };
                    using (var meter = new AudioMeterService()) { meter.Start(instance); var levels = meter.Read(instance.Id); if (Math.Abs(levels.Left - 25.1) > 0.2 || Math.Abs(levels.Right - 25.1) > 0.2) throw new InvalidDataException("VU meter non valido."); }
                    await StatisticsService.Instance.RecordAsync(instance);
                    _ = new InfoWindow();
                    _ = new StatisticsWindow(instance);
                    var testFile = Path.Combine(Path.GetTempPath(), $"StreamPAL-{Guid.NewGuid():N}.xlsx");
                    StatisticsExcelExporter.Export(testFile, instance, StatisticsService.Instance.Get(instance));
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(testFile)) { if (archive.Entries.Count < 6) throw new InvalidDataException("Esportazione Excel incompleta."); }
                    File.Delete(testFile);
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "StreamPAL-smoke-test-error.txt"), ex.ToString()); } catch { }
                    Shutdown(2);
                }
            });
        };
        DispatcherUnhandledException += (_, e) =>
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamPAL", "logs");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "errors.log"), $"[{DateTime.Now:O}] {e.Exception}\n\n");
            }
            catch { }
            MessageBox.Show($"Si è verificato un errore, ma StreamPAL può continuare.\n\n{e.Exception.Message}", "StreamPAL", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }
    private static async Task ListenForSmokeAsync(string url, CancellationToken token)
    {
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan }; using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode(); await using var stream = await response.Content.ReadAsStreamAsync(token); var buffer = new byte[4096]; while (!token.IsCancellationRequested) await stream.ReadAsync(buffer, token);
    }
}
