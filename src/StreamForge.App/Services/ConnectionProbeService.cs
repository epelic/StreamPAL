using System.Net.Sockets;
using StreamForge.Models;

namespace StreamForge.Services;
public static class ConnectionProbeService
{
    public static async Task<bool> IsReachableAsync(EncoderProfile encoder, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(encoder.Host, encoder.Port, cancellationToken);
        return client.Connected;
    }
}
