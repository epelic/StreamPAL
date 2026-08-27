using System.Collections.Concurrent; using System.Diagnostics; using System.Net.Sockets; using System.Text; using System.Threading.Channels;
namespace StreamPAL.Linux;
public sealed class LinuxAudioEngine:IDisposable
{
    private sealed class RunningSource{public required Process Process;public required CancellationTokenSource Stop;public required Task Pump;public ConcurrentDictionary<Guid,LinuxEncoderSession> Outputs=new();}
    private readonly ConcurrentDictionary<Guid,RunningSource> _sources=new();
    public event Action<Guid,double,double>? LevelsUpdated;
    public void Start(SourceInstance instance)
    {
        Stop(instance.Id);var stop=new CancellationTokenSource();var process=CreateSource(instance);var running=new RunningSource{Process=process,Stop=stop,Pump=Task.CompletedTask};_sources[instance.Id]=running;running.Pump=Task.Run(async()=>{var buffer=new byte[instance.InputSampleRate*4/20];try{while(!stop.IsCancellationRequested){var read=await process.StandardOutput.BaseStream.ReadAsync(buffer,stop.Token);if(read<=0)throw new IOException("La sorgente audio si è interrotta.");var copy=buffer[..read];var levels=Measure(copy);LevelsUpdated?.Invoke(instance.Id,levels.Left,levels.Right);foreach(var output in running.Outputs.Values)output.Feed(copy);}}catch(OperationCanceledException){}catch(Exception ex){foreach(var e in instance.Encoders.Where(x=>x.IsRunning))e.AddLog(ex.Message+" Riconnessione tra 5 secondi…");await ReconnectSourceAsync(instance,stop.Token);}},stop.Token);
    }
    public void StartEncoder(SourceInstance source,EncoderProfile encoder){if(!_sources.TryGetValue(source.Id,out var running)){Start(source);running=_sources[source.Id];}if(running.Outputs.TryRemove(encoder.Id,out var old))old.Dispose();var session=new LinuxEncoderSession(encoder,source.InputSampleRate);running.Outputs[encoder.Id]=session;encoder.IsRunning=true;}
    public void StopEncoder(SourceInstance source,EncoderProfile encoder){encoder.IsRunning=false;encoder.IsConnected=false;if(_sources.TryGetValue(source.Id,out var running)&&running.Outputs.TryRemove(encoder.Id,out var session))session.Dispose();}
    private async Task ReconnectSourceAsync(SourceInstance source,CancellationToken token){while(!token.IsCancellationRequested){try{await Task.Delay(5000,token);Start(source);return;}catch(OperationCanceledException){return;}catch{}}}
    private static Process CreateSource(SourceInstance s)
    {
        string file,args;if(s.SourceType=="PipeWire"){file="pw-record";args=$"--format s16 --rate {s.InputSampleRate} --channels 2 --target {Q(s.Source)} -";}else if(s.SourceType=="JACK"){file="pw-record";args=$"--format s16 --rate {s.InputSampleRate} --channels 2 --target {Q(s.Source)} -";}else if(s.SourceType=="Test tone"){file="ffmpeg";args=$"-hide_banner -loglevel error -re -f lavfi -i sine=frequency=1000:sample_rate={s.InputSampleRate} -ac 2 -f s16le pipe:1";}else{file="ffmpeg";args=$"-hide_banner -loglevel error -re -i {Q(s.Source)} -ar {s.InputSampleRate} -ac 2 -f s16le pipe:1";}var p=new Process{StartInfo=new(file,args){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}};if(!p.Start())throw new InvalidOperationException("Impossibile aprire la sorgente.");return p;
    }
    public void Stop(Guid id){if(!_sources.TryRemove(id,out var r))return;r.Stop.Cancel();foreach(var o in r.Outputs.Values)o.Dispose();TryKill(r.Process);r.Stop.Dispose();}
    private static string Q(string value)=>"\""+value.Replace("\"","\\\"")+"\"";internal static void TryKill(Process p){try{if(!p.HasExited)p.Kill(true);}catch{}p.Dispose();}
    private static (double Left,double Right) Measure(byte[] pcm){var left=0;var right=0;for(var i=0;i+3<pcm.Length;i+=4){var l=Math.Abs((int)(short)(pcm[i]|pcm[i+1]<<8));var r=Math.Abs((int)(short)(pcm[i+2]|pcm[i+3]<<8));if(l>left)left=l;if(r>right)right=r;}return(Math.Min(100,left/327.68),Math.Min(100,right/327.68));}
    public void Dispose(){foreach(var id in _sources.Keys)Stop(id);}
}

public sealed class LinuxEncoderSession:IDisposable
{
    private readonly EncoderProfile _encoder;private readonly int _inputRate;private readonly int _outputChannels;private readonly Channel<byte[]> _queue=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(120){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true});private readonly CancellationTokenSource _stop=new();private readonly Task _worker;
    public LinuxEncoderSession(EncoderProfile encoder,int inputRate){_encoder=encoder;_inputRate=inputRate;_outputChannels=encoder.OutputMode.Equals("Mono",StringComparison.OrdinalIgnoreCase)||encoder.ChannelMode=="Mono (L+R)"?1:2;_worker=Task.Run(RunAsync);}
    public void Feed(byte[] pcm)=>_queue.Writer.TryWrite(Route(pcm));
    private byte[] Route(byte[] pcm)
    {
        if(_outputChannels==2&&_encoder.ChannelMode=="Stereo")return pcm;
        var frames=pcm.Length/4;var output=new byte[frames*_outputChannels*2];
        for(var f=0;f<frames;f++){var i=f*4;var l=(short)(pcm[i]|pcm[i+1]<<8);var r=(short)(pcm[i+2]|pcm[i+3]<<8);var selected=_encoder.ChannelMode=="Solo sinistro"?l:_encoder.ChannelMode=="Solo destro"?r:(short)(((int)l+r)/2);if(_outputChannels==1){output[f*2]=(byte)selected;output[f*2+1]=(byte)(selected>>8);}else{var o=f*4;output[o]=(byte)selected;output[o+1]=(byte)(selected>>8);output[o+2]=(byte)selected;output[o+3]=(byte)(selected>>8);}}
        return output;
    }
    private async Task RunAsync(){var delay=2;while(!_stop.IsCancellationRequested){try{await StreamOnceAsync(_stop.Token);delay=2;}catch(OperationCanceledException){break;}catch(Exception ex){_encoder.IsConnected=false;_encoder.AddLog($"Connessione interrotta: {ex.Message}. Riprovo tra {delay}s…");await Task.Delay(TimeSpan.FromSeconds(delay),_stop.Token);delay=Math.Min(delay*2,30);}}}
    private async Task StreamOnceAsync(CancellationToken token)
    {
        using var tcp=new TcpClient{NoDelay=false};await tcp.ConnectAsync(_encoder.Host,_encoder.Port,token);using var network=tcp.GetStream();await Handshake(network,token);using var codec=CreateCodec();_encoder.IsConnected=true;_encoder.AddLog($"Streaming {_encoder.Codec} {_encoder.OutputMode} attivo · buffer 6 s");var copy=codec.StandardOutput.BaseStream.CopyToAsync(network,token);await foreach(var block in _queue.Reader.ReadAllAsync(token)){await codec.StandardInput.BaseStream.WriteAsync(block,token);if(copy.IsCompleted)await copy;}await copy;
    }
    private Process CreateCodec()
    {
        string program;
        string args;
        if (_encoder.Codec == "AAC+ (HE-AAC)")
        {
            program = "/bin/sh";
            var profile = _outputChannels == 2 && _encoder.BitrateKbps <= 24 ? 29 : 5;
            args = $"-c \"ffmpeg -hide_banner -loglevel error -f s16le -ar {_inputRate} -ac {_outputChannels} -i pipe:0 -ar {_encoder.SampleRate} -ac {_outputChannels} -f s16le pipe:1 | fdkaac -S -R --raw-channels {_outputChannels} --raw-rate {_encoder.SampleRate} --raw-format S16L -p {profile} -s 2 -a 1 -b {_encoder.BitrateKbps * 1000} -f 2 -o - -\"";
        }
        else
        {
            var codec = _encoder.Codec switch
            {
                "MP3" => "-c:a libmp3lame -f mp3",
                "AAC-LC" => "-c:a aac -profile:a aac_low -f adts",
                "OGG Vorbis" => "-c:a libvorbis -f ogg",
                "Opus" => "-c:a libopus -f opus",
                _ => throw new NotSupportedException($"Codec non supportato: {_encoder.Codec}")
            };
            program = "ffmpeg";
            args = $"-hide_banner -loglevel error -f s16le -ar {_inputRate} -ac {_outputChannels} -i pipe:0 -ar {_encoder.SampleRate} -ac {_outputChannels} -b:a {_encoder.BitrateKbps}k {codec} pipe:1";
        }
        var p = new Process { StartInfo = new(program, args) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        if (!p.Start()) throw new InvalidOperationException("Codec non avviato");
        return p;
    }
    private async Task Handshake(NetworkStream s,CancellationToken token){var content=_encoder.Codec switch{"MP3"=>"audio/mpeg","AAC-LC"=>"audio/aac","AAC+ (HE-AAC)"=>"audio/aacp",_=>"application/ogg"};string req;if(_encoder.ServerType=="Icecast 2"){var mount=_encoder.Mount.StartsWith('/')?_encoder.Mount:"/"+_encoder.Mount;var auth=Convert.ToBase64String(Encoding.UTF8.GetBytes($"source:{_encoder.Password}"));req=$"PUT {mount} HTTP/1.1\r\nHost: {_encoder.Host}:{_encoder.Port}\r\nAuthorization: Basic {auth}\r\nUser-Agent: StreamPAL-Linux/1.0.0\r\nContent-Type: {content}\r\nIce-Name: {_encoder.StationName}\r\nIce-Public: 0\r\n\r\n";}else req=$"{_encoder.Password}\r\nicy-name:{_encoder.StationName}\r\nicy-pub:0\r\nicy-br:{_encoder.BitrateKbps}\r\ncontent-type:{content}\r\n\r\n";await s.WriteAsync(Encoding.UTF8.GetBytes(req),token);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(8));var b=new byte[1024];var read=await s.ReadAsync(b,timeout.Token);var text=Encoding.ASCII.GetString(b,0,read);_encoder.AddLog("Server: "+text.Split('\r','\n',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());if(!(text.Contains("200")||text.Contains("OK2")||text.StartsWith("OK")))throw new IOException(text.Trim());}
    public void Dispose(){_stop.Cancel();_queue.Writer.TryComplete();try{_worker.Wait(1500);}catch{}_stop.Dispose();}
}
