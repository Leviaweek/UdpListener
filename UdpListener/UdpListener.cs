using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace UdpListener;

public sealed class UdpListener(int port): IDisposable
{
    private readonly UdpClient _client = new(port);
    private readonly ConcurrentDictionary<IPEndPoint, UdpSession> _clients = new();
    private readonly Channel<IPEndPoint> _uniqueUdpSessions = Channel.CreateUnbounded<IPEndPoint>();

    private async Task ReceiveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var result = await _client.ReceiveAsync(token);
            var buffer = result.Buffer;

            if (_clients.TryGetValue(result.RemoteEndPoint, out var session))
            { 
                await session.EnqueueAsync(buffer);
                continue;
            }

            session = new UdpSession(result.RemoteEndPoint, _client, () => _clients.TryRemove(result.RemoteEndPoint, out _));
            _clients.TryAdd(result.RemoteEndPoint, session);
            await session.EnqueueAsync(buffer);
            await _uniqueUdpSessions.Writer.WriteAsync(result.RemoteEndPoint, token);
        }
    }

    public void Start(CancellationToken cancellationToken) => _ = ReceiveLoop(cancellationToken);

    public async Task<UdpSession> AcceptUdpSessionAsync(CancellationToken cancellationToken)
    {
        var endPoint = await _uniqueUdpSessions.Reader.ReadAsync(cancellationToken);
        return _clients[endPoint];
    }

    public void Dispose()
    {
        foreach (var client in _clients.Values)
            client.Dispose();
        
        _client.Dispose();
    }
}