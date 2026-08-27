using System.Runtime.InteropServices;
namespace StreamForge.Services;
internal sealed class FdkAacEncoder : IDisposable
{
    private IntPtr _handle; public int InputSamplesPerFrame { get; }
    public FdkAacEncoder(int sampleRate, int channels, int bitrate, bool he)
    {
        Check(Native.aacEncOpen(out _handle, 0, (uint)channels), "apertura");
        try { var audioObjectType = he && channels == 2 && bitrate <= 48000 ? 29u : he ? 5u : 2u; Set(0x0100, audioObjectType); Set(0x0101, (uint)bitrate); Set(0x0103, (uint)sampleRate); Set(0x0106, channels == 1 ? 1u : 2u); Set(0x0300, 2); Set(0x0200, 1); Check(Native.aacEncEncode(_handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), "inizializzazione"); var info = new Info { ConfBuf = new byte[64] }; Check(Native.aacEncInfo(_handle, ref info), "configurazione"); InputSamplesPerFrame = checked((int)(info.FrameLength * info.InputChannels)); if (InputSamplesPerFrame <= 0) throw new InvalidOperationException("Frame AAC non valido."); } catch { Dispose(); throw; }
    }
    public byte[] Encode(short[] pcm)
    {
        var output = new byte[16384]; var pcmPin = GCHandle.Alloc(pcm, GCHandleType.Pinned); var outPin = GCHandle.Alloc(output, GCHandleType.Pinned); var inPtrs = new[] { pcmPin.AddrOfPinnedObject() }; var outPtrs = new[] { outPin.AddrOfPinnedObject() }; var inIds = new[] { 0 }; var outIds = new[] { 3 }; var inSizes = new[] { pcm.Length * 2 }; var outSizes = new[] { output.Length }; var inEls = new[] { 2 }; var outEls = new[] { 1 };
        var pins = new[] { GCHandle.Alloc(inPtrs, GCHandleType.Pinned), GCHandle.Alloc(outPtrs, GCHandleType.Pinned), GCHandle.Alloc(inIds, GCHandleType.Pinned), GCHandle.Alloc(outIds, GCHandleType.Pinned), GCHandle.Alloc(inSizes, GCHandleType.Pinned), GCHandle.Alloc(outSizes, GCHandleType.Pinned), GCHandle.Alloc(inEls, GCHandleType.Pinned), GCHandle.Alloc(outEls, GCHandleType.Pinned) };
        try { var input = new BufferDesc { NumBufs=1, Bufs=pins[0].AddrOfPinnedObject(), BufferIdentifiers=pins[2].AddrOfPinnedObject(), BufSizes=pins[4].AddrOfPinnedObject(), BufElSizes=pins[6].AddrOfPinnedObject() }; var outDesc = new BufferDesc { NumBufs=1, Bufs=pins[1].AddrOfPinnedObject(), BufferIdentifiers=pins[3].AddrOfPinnedObject(), BufSizes=pins[5].AddrOfPinnedObject(), BufElSizes=pins[7].AddrOfPinnedObject() }; var args = new InArgs { NumInSamples=pcm.Length }; var result = new OutArgs(); Check(Native.aacEncEncode(_handle, ref input, ref outDesc, ref args, ref result), "codifica"); return result.NumOutBytes <= 0 ? [] : output[..result.NumOutBytes]; } finally { foreach (var p in pins) p.Free(); pcmPin.Free(); outPin.Free(); }
    }
    private void Set(int p,uint v)=>Check(Native.aacEncoder_SetParam(_handle,p,v),$"parametro 0x{p:X}"); private static void Check(int c,string op){if(c!=0)throw new InvalidOperationException($"FDK AAC: errore 0x{c:X} durante {op}.");}
    public void Dispose(){if(_handle==IntPtr.Zero)return;Native.aacEncClose(ref _handle);_handle=IntPtr.Zero;}
    [StructLayout(LayoutKind.Sequential)] private struct BufferDesc{public int NumBufs;public IntPtr Bufs,BufferIdentifiers,BufSizes,BufElSizes;}
    [StructLayout(LayoutKind.Sequential)] private struct InArgs{public int NumInSamples,NumAncBytes;}
    [StructLayout(LayoutKind.Sequential)] private struct OutArgs{public int NumOutBytes,NumInSamples,NumAncBytes,BitResState;}
    [StructLayout(LayoutKind.Sequential)] private struct Info{public uint MaxOutBufBytes,MaxAncBytes,InBufFillLevel,InputChannels,FrameLength,NDelay,NDelayCore;[MarshalAs(UnmanagedType.ByValArray,SizeConst=64)]public byte[] ConfBuf;public uint ConfSize;}
    private static class Native{[DllImport("libAACenc.dll",CallingConvention=CallingConvention.Cdecl)]internal static extern int aacEncOpen(out IntPtr h,uint m,uint c);[DllImport("libAACenc.dll",CallingConvention=CallingConvention.Cdecl)]internal static extern int aacEncoder_SetParam(IntPtr h,int p,uint v);[DllImport("libAACenc.dll",CallingConvention=CallingConvention.Cdecl)]internal static extern int aacEncEncode(IntPtr h,IntPtr i,IntPtr o,IntPtr ia,IntPtr oa);[DllImport("libAACenc.dll",CallingConvention=CallingConvention.Cdecl)]internal static extern int aacEncEncode(IntPtr h,ref BufferDesc i,ref BufferDesc o,ref InArgs ia,ref OutArgs oa);[DllImport("libAACenc.dll",CallingConvention=CallingConvention.Cdecl)]internal static extern int aacEncInfo(IntPtr h,ref Info i);[DllImport("libAACenc.dll",CallingConvention=CallingConvention.Cdecl)]internal static extern int aacEncClose(ref IntPtr h);}
}
