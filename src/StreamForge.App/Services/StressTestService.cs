using System.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace StreamForge.Services;
public sealed record StressTestResult(int Instances, int Encoders, long PcmBytesProcessed, TimeSpan Elapsed, long MemoryDelta);
public static class StressTestService
{
    public static Task<StressTestResult> RunAsync() => Task.Run(() =>
    {
        const int instances = 64, encodersPerInstance = 4, cycles = 30; var format = new WaveFormat(48000, 16, 2); var before = GC.GetTotalMemory(true); var watch = Stopwatch.StartNew(); long processed = 0;
        Parallel.For(0, instances, _ =>
        {
            var pipelines = Enumerable.Range(0, encodersPerInstance).Select(i =>
            {
                var buffer = new BufferedWaveProvider(format) { BufferDuration = TimeSpan.FromSeconds(2), DiscardOnBufferOverflow = false, ReadFully = false };
                var routed = new RoutingSampleProvider(buffer.ToSampleProvider(), i switch { 1 => "Solo sinistro", 2 => "Solo destro", 3 => "Mono (L+R)", _ => "Stereo" }); var output = new SampleToWaveProvider16(new WdlResamplingSampleProvider(routed, i % 2 == 0 ? 44100 : 48000)); return (buffer, output, readBuffer: new byte[38400]);
            }).ToArray();
            var source = new byte[19200]; for (var i = 0; i < source.Length; i += 2) { var sample = (short)(Math.Sin(i / 4d * Math.PI / 24) * 8000); source[i] = (byte)sample; source[i + 1] = (byte)(sample >> 8); }
            long local = 0; for (var cycle = 0; cycle < cycles; cycle++) foreach (var pipe in pipelines) { pipe.buffer.AddSamples(source, 0, source.Length); int read; do { read = pipe.output.Read(pipe.readBuffer, 0, pipe.readBuffer.Length); local += read; } while (read > 0); } Interlocked.Add(ref processed, local);
        });
        watch.Stop(); var after = GC.GetTotalMemory(true); return new StressTestResult(instances, instances * encodersPerInstance, processed, watch.Elapsed, Math.Max(0, after - before));
    });
}
