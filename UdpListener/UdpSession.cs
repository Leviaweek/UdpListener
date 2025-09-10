using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace UdpListener;

public sealed class UdpSession(IPEndPoint endPoint, UdpClient client, Action destroy): IDisposable
{
    private readonly Channel<byte[]> _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    
    public ValueTask<byte[]> ReceiveAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAsync(cancellationToken);

    public ValueTask<int> SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellation) =>
        client.SendAsync(message, endPoint, cancellation);
    
    public ValueTask EnqueueAsync(byte[] buffer) => _channel.Writer.WriteAsync(buffer);
    
    public void Dispose()
    {
        _channel.Writer.Complete();
        destroy();
    }
}