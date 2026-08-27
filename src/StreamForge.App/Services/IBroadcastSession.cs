namespace StreamForge.Services;
public interface IBroadcastSession : IDisposable { bool IsCompleted { get; } void Feed(byte[] data); }
