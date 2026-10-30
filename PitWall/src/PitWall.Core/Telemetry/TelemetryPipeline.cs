using System.Threading.Channels;
using PitWall.Core.Net;
using PitWall.Core.Recording;
using PitWall.Core.State;
using Serilog;

namespace PitWall.Core.Telemetry;

public sealed class TelemetryPipeline : IAsyncDisposable
{
    private readonly Channel<byte[]> _channel;
    private readonly UdpReceiver _receiver;
    private readonly PacketDecoder _decoder;
    private readonly StateStore _store;
    private readonly Recorder _recorder;
    private readonly ILogger _log;
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;
    private Task? _processTask;

    public TelemetryPipeline(int udpPort, StateStore store, Recorder recorder, ILogger? log = null)
    {
        _channel = TelemetryChannel.Create();
        _receiver = new UdpReceiver(udpPort, _channel, log);
        _decoder = new PacketDecoder(log);
        _store = store;
        _recorder = recorder;
        _log = log ?? Log.Logger;
    }

    public UdpReceiver Receiver => _receiver;
    public PacketDecoder Decoder => _decoder;

    public void Start()
    {
        _receiver.Bind();
        _receiveTask = Task.Run(() => _receiver.RunAsync(_cts.Token));
        _processTask = Task.Run(() => ProcessAsync(_cts.Token));
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var datagram in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var now = DateTimeOffset.UtcNow;
                _recorder.Write(datagram, now);
                if (_decoder.TryDecode(datagram, out var packet, out var status))
                {
                    if (status == DecodeStatus.NewSession)
                    {
                        _store.Reset();
                    }

                    _store.Apply(packet, now);
                }

                _store.NoteStats(_receiver.Received, _receiver.Dropped, _decoder.Malformed + _decoder.DroppedOld);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Telemetry processing loop failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _channel.Writer.TryComplete();
        if (_receiveTask is not null)
        {
            try { await _receiveTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }

        if (_processTask is not null)
        {
            try { await _processTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }

        await _receiver.DisposeAsync();
        _cts.Dispose();
    }
}
