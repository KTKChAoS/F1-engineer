using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using PitWall.Contracts;
using Serilog;

namespace PitWall.Core.Net;

public sealed class UdpReceiver : IAsyncDisposable
{
    private readonly int _port;
    private readonly Channel<byte[]> _channel;
    private readonly ILogger _log;
    private UdpClient? _client;
    private long _received;
    private long _dropped;

    public UdpReceiver(int port, Channel<byte[]> channel, ILogger? log = null)
    {
        _port = port;
        _channel = channel;
        _log = log ?? Log.Logger;
    }

    public long Received => Interlocked.Read(ref _received);
    public long Dropped => Interlocked.Read(ref _dropped);

    public void Bind()
    {
        try
        {
            _client = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
            _client.Client.ReceiveBufferSize = 1 << 20;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            throw new InvalidOperationException(
                $"UDP port {_port} is already in use. Close the other listener or pick a different port.", ex);
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            Bind();
        }

        _log.Information("UDP receiver listening on 0.0.0.0:{Port}", _port);

        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult datagram;
            try
            {
                datagram = await _client!.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                _log.Debug(ex, "UDP receive error");
                continue;
            }

            Interlocked.Increment(ref _received);
            var payload = datagram.Buffer;
            if (_channel.Writer.TryWrite(payload))
            {
                continue;
            }

            _channel.Reader.TryRead(out _);
            Interlocked.Increment(ref _dropped);
            _channel.Writer.TryWrite(payload);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }
}

public static class TelemetryChannel
{
    public static Channel<byte[]> Create(int capacity = Constants.UdpChannelCapacity)
        => Channel.CreateBounded<byte[]>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
}
