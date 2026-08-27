namespace StreamForge.Models;

public sealed class ListenerSample
{
    public DateTime TimestampUtc { get; set; }
    public Dictionary<Guid, int> Streams { get; set; } = [];
    public int Total => Streams.Values.Sum();
}
