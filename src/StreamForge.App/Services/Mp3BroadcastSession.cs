using System.IO;
using System.Net.Sockets;
using System.Text;
using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using StreamForge.Models;

namespace StreamForge.Services;
public sealed class Mp3BroadcastSession : IBroadcastSession
{
    private readonly EncoderProfile _encoder; private readonly BufferedWaveProvider _buffer; private readonly CancellationTokenSource _stop = new(); private readonly Task _worker;
    public Mp3BroadcastSession(EncoderProfile encoder, WaveFormat sourceFormat)
    {
        _encoder = encoder; _buffer = new BufferedWaveProvider(sourceFormat) { BufferDuration = TimeSpan.FromSeconds(2), DiscardOnBufferOverflow = true, ReadFully = false };
        _worker = Task.Run(RunAsync);
    }
    public bool IsCompleted => _worker.IsCompleted;
    public void Feed(byte[] data) { if (!_stop.IsCancellationRequested) _buffer.AddSamples(data, 0, data.Length); }
    private async Task RunAsync()
    {
        try
        {
            _encoder.AddLog("Connessione al server sorgente…"); using var tcp = new TcpClient(); await tcp.ConnectAsync(_encoder.Host, _encoder.Port, _stop.Token); using var network = tcp.GetStream();
            await HandshakeAsync(network, _stop.Token); _buffer.ClearBuffer(); _encoder.IsConnected = true; _encoder.AddLog("Handshake accettato: streaming MP3 attivo");
            var samples = new RoutingSampleProvider(_buffer.ToSampleProvider(), _encoder.ChannelMode, _encoder.OutputMode); var resampled = new WdlResamplingSampleProvider(samples, _encoder.SampleRate); var pcm = new SampleToWaveProvider16(resampled);
            using var lame = new LameMP3FileWriter(network, pcm.WaveFormat, _encoder.BitrateKbps); var block = new byte[Math.Max(4096, pcm.WaveFormat.AverageBytesPerSecond / 10)]; long sent = 0; var nextLog = DateTime.UtcNow.AddSeconds(10);
            while (!_stop.IsCancellationRequested) { var read = pcm.Read(block, 0, block.Length); if (read == 0) { await Task.Delay(10, _stop.Token); continue; } lame.Write(block, 0, read); sent += read; if (DateTime.UtcNow >= nextLog) { _encoder.AddLog($"Audio inviato: {sent / 1024:N0} KiB PCM codificati"); nextLog = DateTime.UtcNow.AddSeconds(10); } }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _encoder.IsConnected = false; _encoder.AddLog($"ERRORE streaming: {ex.Message}"); }
        finally { _encoder.IsConnected = false; }
    }
    private async Task HandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        string request;
        if (_encoder.ServerType == "Icecast 2")
        {
            var mount = _encoder.Mount.StartsWith('/') ? _encoder.Mount : "/" + _encoder.Mount; var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"source:{_encoder.Password}"));
            request = $"PUT {mount} HTTP/1.1\r\nHost: {_encoder.Host}:{_encoder.Port}\r\nAuthorization: Basic {auth}\r\nUser-Agent: StreamPAL/1.0.0\r\nContent-Type: audio/mpeg\r\nIce-Name: {_encoder.StationName}\r\nIce-Description: {_encoder.Description}\r\nIce-Genre: {_encoder.Genre}\r\nIce-URL: {_encoder.StationUrl}\r\nIce-Public: 0\r\n\r\n";
        }
        else request = $"{_encoder.Password}\r\nicy-name:{_encoder.StationName}\r\nicy-genre:{_encoder.Genre}\r\nicy-url:{_encoder.StationUrl}\r\nicy-pub:0\r\nicy-br:{_encoder.BitrateKbps}\r\ncontent-type:audio/mpeg\r\n\r\n";
        var bytes = Encoding.UTF8.GetBytes(request); await stream.WriteAsync(bytes, token); await stream.FlushAsync(token); var response = new byte[1024]; using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(8)); var read = await stream.ReadAsync(response, timeout.Token); var text = Encoding.ASCII.GetString(response, 0, read); _encoder.AddLog("Server: " + text.Split('\r','\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());
        if (!(text.Contains("200") || text.Contains("OK2") || text.StartsWith("OK"))) throw new IOException("Autenticazione rifiutata: " + text.Trim());
    }
    public void Dispose() { _stop.Cancel(); try { _worker.Wait(1500); } catch { } _stop.Dispose(); }
}
