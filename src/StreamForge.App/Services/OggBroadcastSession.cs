using System.IO;
using System.Net.Sockets;
using System.Text;
using TextEncoding = System.Text.Encoding;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OggVorbisEncoder;
using StreamForge.Models;

namespace StreamForge.Services;

public sealed class OggBroadcastSession : IBroadcastSession
{
    private readonly EncoderProfile _encoder; private readonly BufferedWaveProvider _buffer; private readonly CancellationTokenSource _stop = new(); private readonly Task _worker;
    public OggBroadcastSession(EncoderProfile encoder, WaveFormat sourceFormat) { _encoder = encoder; _buffer = new BufferedWaveProvider(sourceFormat) { BufferDuration = TimeSpan.FromSeconds(2), DiscardOnBufferOverflow = true, ReadFully = false }; _worker = Task.Run(RunAsync); }
    public bool IsCompleted => _worker.IsCompleted;
    public void Feed(byte[] data) { if (!_stop.IsCancellationRequested) _buffer.AddSamples(data, 0, data.Length); }

    private async Task RunAsync()
    {
        try
        {
            _encoder.AddLog("Connessione al server sorgente…"); using var tcp = new TcpClient(); await tcp.ConnectAsync(_encoder.Host, _encoder.Port, _stop.Token); using var network = tcp.GetStream(); await HandshakeAsync(network, _stop.Token);
            _buffer.ClearBuffer(); _encoder.IsConnected = true; _encoder.AddLog("Handshake accettato: streaming OGG Vorbis attivo");
            var provider = new WdlResamplingSampleProvider(new RoutingSampleProvider(_buffer.ToSampleProvider(), _encoder.ChannelMode), _encoder.SampleRate); var channels = provider.WaveFormat.Channels;
            var quality = Math.Clamp((_encoder.BitrateKbps - 32) / 160f, 0.05f, 1f); var info = VorbisInfo.InitVariableBitRate(channels, _encoder.SampleRate, quality); var ogg = new OggStream(Random.Shared.Next()); var comments = new Comments(); comments.AddTag("TITLE", _encoder.Metadata); comments.AddTag("ARTIST", _encoder.StationName);
            ogg.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info)); ogg.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments)); ogg.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info)); Flush(ogg, network, true);
            var state = ProcessingState.Create(info); var interleaved = new float[1024 * channels]; var planar = Enumerable.Range(0, channels).Select(_ => new float[1024]).ToArray();
            while (!_stop.IsCancellationRequested)
            {
                var read = provider.Read(interleaved, 0, interleaved.Length); if (read == 0) { await Task.Delay(10, _stop.Token); continue; } var frames = read / channels;
                for (var frame = 0; frame < frames; frame++) for (var channel = 0; channel < channels; channel++) planar[channel][frame] = interleaved[frame * channels + channel];
                state.WriteData(planar, frames, 0); while (!ogg.Finished && state.PacketOut(out var packet)) { ogg.PacketIn(packet); Flush(ogg, network, false); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _encoder.AddLog($"ERRORE streaming OGG: {ex.Message}"); }
        finally { _encoder.IsConnected = false; }
    }

    private static void Flush(OggStream ogg, Stream output, bool force) { while (ogg.PageOut(out var page, force)) { output.Write(page.Header); output.Write(page.Body); output.Flush(); } }
    private async Task HandshakeAsync(NetworkStream stream, CancellationToken token)
    {
        string request; if (_encoder.ServerType == "Icecast 2") { var mount = _encoder.Mount.StartsWith('/') ? _encoder.Mount : "/" + _encoder.Mount; var auth = Convert.ToBase64String(TextEncoding.UTF8.GetBytes($"source:{_encoder.Password}")); request = $"PUT {mount} HTTP/1.1\r\nHost: {_encoder.Host}:{_encoder.Port}\r\nAuthorization: Basic {auth}\r\nUser-Agent: StreamPAL/1.0.0\r\nContent-Type: application/ogg\r\nIce-Name: {_encoder.StationName}\r\nIce-Public: 0\r\n\r\n"; } else request = $"{_encoder.Password}\r\nicy-name:{_encoder.StationName}\r\nicy-pub:0\r\nicy-br:{_encoder.BitrateKbps}\r\ncontent-type:application/ogg\r\n\r\n";
        await stream.WriteAsync(TextEncoding.UTF8.GetBytes(request), token); await stream.FlushAsync(token); var response = new byte[1024]; using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(8)); var read = await stream.ReadAsync(response, timeout.Token); var text = TextEncoding.ASCII.GetString(response, 0, read); _encoder.AddLog("Server: " + text.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()); if (!(text.Contains("200") || text.Contains("OK2") || text.StartsWith("OK"))) throw new IOException("Autenticazione rifiutata: " + text.Trim());
    }
    public void Dispose() { _stop.Cancel(); try { _worker.Wait(1500); } catch { } _stop.Dispose(); }
}
