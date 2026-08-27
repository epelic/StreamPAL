using System.IO;
using System.Net.Sockets;
using System.Text;
using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using StreamForge.Models;

namespace StreamForge.Services;

public sealed class OpusBroadcastSession : IBroadcastSession
{
    private readonly EncoderProfile _encoder;
    private readonly BufferedWaveProvider _buffer;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;

    public OpusBroadcastSession(EncoderProfile encoder, WaveFormat sourceFormat)
    {
        _encoder = encoder;
        _buffer = new BufferedWaveProvider(sourceFormat) { BufferDuration = TimeSpan.FromSeconds(2), DiscardOnBufferOverflow = true, ReadFully = false };
        _worker = Task.Run(RunAsync);
    }
    public bool IsCompleted => _worker.IsCompleted;

    public void Feed(byte[] data) { if (!_stop.IsCancellationRequested) _buffer.AddSamples(data, 0, data.Length); }

    private async Task RunAsync()
    {
        try
        {
            _encoder.AddLog("Connessione al server sorgente…");
            using var tcp = new TcpClient(); await tcp.ConnectAsync(_encoder.Host, _encoder.Port, _stop.Token); using var network = tcp.GetStream();
            await HandshakeAsync(network, _stop.Token); _buffer.ClearBuffer(); _encoder.IsConnected = true; _encoder.AddLog("Handshake accettato: streaming Opus attivo");
            var routed = new RoutingSampleProvider(_buffer.ToSampleProvider(), _encoder.ChannelMode, _encoder.OutputMode);
            var resampled = new WdlResamplingSampleProvider(routed, 48000);
            var pcm = new SampleToWaveProvider16(resampled);
            var opusEncoder = OpusCodecFactory.CreateEncoder(48000, pcm.WaveFormat.Channels, OpusApplication.OPUS_APPLICATION_AUDIO);
            opusEncoder.Bitrate = _encoder.BitrateKbps * 1000;
            var tags = new OpusTags(); tags.Fields[OpusTagName.Title] = _encoder.StationName;
            var ogg = new OpusOggWriteStream(opusEncoder, network, tags, 48000, leaveOpen: true);
            var bytes = new byte[3840]; var samples = new short[1920];
            while (!_stop.IsCancellationRequested)
            {
                var read = pcm.Read(bytes, 0, bytes.Length);
                if (read == 0) { await Task.Delay(10, _stop.Token); continue; }
                Buffer.BlockCopy(bytes, 0, samples, 0, read); ogg.WriteSamples(samples, 0, read / 2);
            }
            ogg.Finish();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _encoder.AddLog($"ERRORE streaming Opus: {ex.Message}"); }
        finally { _encoder.IsConnected = false; }
    }

    private async Task HandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        string request;
        if (_encoder.ServerType == "Icecast 2")
        {
            var mount = _encoder.Mount.StartsWith('/') ? _encoder.Mount : "/" + _encoder.Mount;
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"source:{_encoder.Password}"));
            request = $"PUT {mount} HTTP/1.1\r\nHost: {_encoder.Host}:{_encoder.Port}\r\nAuthorization: Basic {auth}\r\nUser-Agent: StreamPAL/1.0.0\r\nContent-Type: audio/ogg\r\nIce-Name: {_encoder.StationName}\r\nIce-Public: 0\r\n\r\n";
        }
        else request = $"{_encoder.Password}\r\nicy-name:{_encoder.StationName}\r\nicy-pub:0\r\nicy-br:{_encoder.BitrateKbps}\r\ncontent-type:audio/ogg\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(request), token); await stream.FlushAsync(token);
        var response = new byte[1024]; using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var read = await stream.ReadAsync(response, timeout.Token); var text = Encoding.ASCII.GetString(response, 0, read);
        _encoder.AddLog("Server: " + text.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());
        if (!(text.Contains("200") || text.Contains("OK2") || text.StartsWith("OK"))) throw new IOException("Autenticazione rifiutata: " + text.Trim());
    }

    public void Dispose() { _stop.Cancel(); try { _worker.Wait(1500); } catch { } _stop.Dispose(); }
}
