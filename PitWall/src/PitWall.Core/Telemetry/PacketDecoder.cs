using F1Game.UDP;
using F1Game.UDP.Enums;
using F1Game.UDP.Packets;
using PitWall.Contracts;
using Serilog;

namespace PitWall.Core.Telemetry;

public sealed class PacketDecoder
{
    private readonly ILogger _log;
    private readonly Dictionary<PacketType, uint> _lastFrame = new();
    private ulong _sessionUid;
    private long _malformed;
    private long _droppedOld;

    public PacketDecoder(ILogger? log = null)
    {
        _log = log ?? Log.Logger;
    }

    public long Malformed => Interlocked.Read(ref _malformed);
    public long DroppedOld => Interlocked.Read(ref _droppedOld);

    public bool TryDecode(ReadOnlySpan<byte> data, out UnionPacket packet, out DecodeStatus status)
    {
        packet = default;
        status = DecodeStatus.Failed;

        if (data.Length < Constants.PacketHeaderSize)
        {
            Interlocked.Increment(ref _malformed);
            return false;
        }

        try
        {
            packet = PacketReader.ToPacket(data);
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Failed to decode {Length}-byte datagram", data.Length);
            Interlocked.Increment(ref _malformed);
            return false;
        }

        var header = packet.Header;
        if (_sessionUid != 0 && header.SessionUID != _sessionUid)
        {
            _lastFrame.Clear();
            status = DecodeStatus.NewSession;
        }
        else
        {
            status = DecodeStatus.Ok;
        }

        _sessionUid = header.SessionUID;

        if (_lastFrame.TryGetValue(header.PacketType, out var last) && header.FrameIdentifier < last)
        {
            Interlocked.Increment(ref _droppedOld);
            status = DecodeStatus.StaleFrame;
            return false;
        }

        _lastFrame[header.PacketType] = header.FrameIdentifier;
        return true;
    }

    public void Reset()
    {
        _lastFrame.Clear();
        _sessionUid = 0;
    }
}

public enum DecodeStatus
{
    Ok,
    NewSession,
    StaleFrame,
    Failed
}
