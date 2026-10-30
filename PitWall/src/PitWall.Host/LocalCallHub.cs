using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using PitWall.Contracts;
using PitWall.Contracts.Enums;
using PitWall.Contracts.Messages;

namespace PitWall.Host;

internal sealed class LocalCallHub
{
    private readonly ConcurrentDictionary<Guid, ClientPeer> _clients = new();
    private readonly ConcurrentDictionary<string, ActiveCall> _calls = new(StringComparer.Ordinal);
    private readonly object _callGate = new();
    private DateTimeOffset _lastCallUtc = DateTimeOffset.MinValue;
    private long _sequence;

    public event Action<CallMessage>? CallReceived;

    public ClientPeer Add(WebSocket socket)
    {
        var peer = new ClientPeer(socket);
        _clients[peer.Id] = peer;
        return peer;
    }

    public void Remove(ClientPeer peer) => _clients.TryRemove(peer.Id, out _);

    public async Task ReceiveAsync(ClientPeer peer, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();
        while (peer.Socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await peer.Socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
            if (result.MessageType != WebSocketMessageType.Text)
                continue;
            if (message.Length + result.Count > Constants.MaxMessageSizeBytes)
            {
                await peer.Socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message exceeds limit", cancellationToken).ConfigureAwait(false);
                break;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
                continue;

            try
            {
                var envelope = JsonSerializer.Deserialize<WireMessage>(message.GetBuffer().AsSpan(0, (int)message.Length), JsonDefaults.Options);
                if (envelope is not null)
                    await HandleAsync(peer, envelope, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                await SendAsync(peer, MessageTypes.Error, new ErrorMessage { Code = "bad_json", Message = "Could not read message." }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                message.SetLength(0);
            }
        }
    }

    public async Task SendSnapshotsAsync(ClientPeer peer, PitWall.Core.State.StateStore store, CancellationToken cancellationToken)
    {
        var counter = 0;
        var lastEventId = 0;
        while (peer.Socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            PruneExpiredCalls();
            var snapshot = store.Current;
            await SendAsync(peer, MessageTypes.Live, PitWall.Core.State.SnapshotBuilder.BuildLive(snapshot), cancellationToken).ConfigureAwait(false);

            if (counter++ % 5 == 0)
                await SendAsync(peer, MessageTypes.Field, PitWall.Core.State.SnapshotBuilder.BuildField(snapshot), cancellationToken).ConfigureAwait(false);

            foreach (var evt in PitWall.Core.State.SnapshotBuilder.BuildEvents(snapshot).Where(item => item.Id > lastEventId))
            {
                lastEventId = Math.Max(lastEventId, evt.Id);
                await SendAsync(peer, MessageTypes.Event, evt, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(Constants.LiveSnapshotIntervalMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(ClientPeer peer, WireMessage message, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case MessageTypes.Hello:
                await HandleHelloAsync(peer, message.Payload.Deserialize<HelloMessage>(JsonDefaults.Options), cancellationToken).ConfigureAwait(false);
                break;
            case MessageTypes.CallMessage:
                await HandleCallAsync(peer, message.Payload.Deserialize<CallMessage>(JsonDefaults.Options), cancellationToken).ConfigureAwait(false);
                break;
            case MessageTypes.CallAck:
                await HandleAckAsync(peer, message.Payload.Deserialize<CallAckMessage>(JsonDefaults.Options), cancellationToken).ConfigureAwait(false);
                break;
            case MessageTypes.Ping:
                var ping = message.Payload.Deserialize<PingMessage>(JsonDefaults.Options);
                if (ping is not null)
                    await SendAsync(peer, MessageTypes.Pong, new PongMessage { Ts = ping.Ts, ServerTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleHelloAsync(ClientPeer peer, HelloMessage? hello, CancellationToken cancellationToken)
    {
        if (hello is null)
            return;
        PruneExpiredCalls();
        peer.Role = hello.Role;
        if (peer.Role == ClientRole.Driver)
        {
            foreach (var pair in _calls)
            {
                await SendAsync(peer, MessageTypes.Call, pair.Value.Message, cancellationToken).ConfigureAwait(false);
                await BroadcastAsync(MessageTypes.CallStatus, new CallStatusMessage { CallId = pair.Key, Status = CallStatus.Delivered }, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            foreach (var pair in _calls)
            {
                var status = HasDriver ? CallStatus.Delivered : CallStatus.Sent;
                await SendAsync(peer, MessageTypes.CallStatus, new CallStatusMessage { CallId = pair.Key, Status = status }, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleCallAsync(ClientPeer peer, CallMessage? call, CancellationToken cancellationToken)
    {
        if (peer.Role != ClientRole.Engineer || call is null || string.IsNullOrWhiteSpace(call.Text) || call.Text.Length > Constants.MaxCallTextLength)
            return;
        if (string.IsNullOrWhiteSpace(call.CallId))
            return;

        PruneExpiredCalls();
        string? rejection = null;
        lock (_callGate)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _lastCallUtc < TimeSpan.FromSeconds(1))
                rejection = "Wait a second before sending another call.";
            else if (_calls.Count >= 3)
                rejection = "The call queue is full.";
            else
            {
                _lastCallUtc = now;
                _calls[call.CallId] = new ActiveCall(call, now);
            }
        }
        if (rejection is not null)
        {
            await SendAsync(peer, MessageTypes.Error, new ErrorMessage { Code = "call_rejected", Message = rejection }, cancellationToken).ConfigureAwait(false);
            return;
        }

        CallReceived?.Invoke(call);

        await BroadcastAsync(MessageTypes.Call, call, cancellationToken).ConfigureAwait(false);
        await BroadcastAsync(MessageTypes.CallStatus, new CallStatusMessage
        {
            CallId = call.CallId,
            Status = HasDriver ? CallStatus.Delivered : CallStatus.Sent
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleAckAsync(ClientPeer peer, CallAckMessage? ack, CancellationToken cancellationToken)
    {
        if (peer.Role != ClientRole.Driver || ack is null || !_calls.TryRemove(ack.CallId, out _))
            return;
        await BroadcastAsync(MessageTypes.CallStatus, new CallStatusMessage
        {
            CallId = ack.CallId,
            Status = CallStatus.Acknowledged
        }, cancellationToken).ConfigureAwait(false);
    }

    private bool HasDriver => _clients.Values.Any(client => client.Role == ClientRole.Driver && client.Socket.State == WebSocketState.Open);

    private void PruneExpiredCalls()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _calls)
        {
            var timeout = pair.Value.Message.Preset is CallPreset.BoxThisLap or CallPreset.BoxNextLap
                ? Constants.BoxCallDismissMs
                : Constants.DefaultCallDismissMs;
            if (now - pair.Value.CreatedUtc >= TimeSpan.FromMilliseconds(timeout))
                _calls.TryRemove(pair.Key, out _);
        }
    }

    private Task BroadcastAsync<T>(string type, T payload, CancellationToken cancellationToken)
        => Task.WhenAll(_clients.Values.Select(peer => SendAsync(peer, type, payload, cancellationToken)));

    private Task SendAsync<T>(ClientPeer peer, string type, T payload, CancellationToken cancellationToken)
        => peer.SendAsync(new WireMessage<T>
        {
            Type = type,
            Seq = Interlocked.Increment(ref _sequence),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = payload
        }, cancellationToken);

    private sealed record ActiveCall(CallMessage Message, DateTimeOffset CreatedUtc);

    internal sealed class ClientPeer(WebSocket socket)
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        public Guid Id { get; } = Guid.NewGuid();
        public WebSocket Socket { get; } = socket;
        public ClientRole? Role { get; set; }

        public async Task SendAsync<T>(WireMessage<T> message, CancellationToken cancellationToken)
        {
            if (Socket.State != WebSocketState.Open)
                return;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonDefaults.Options);
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (Socket.State == WebSocketState.Open)
                    await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
