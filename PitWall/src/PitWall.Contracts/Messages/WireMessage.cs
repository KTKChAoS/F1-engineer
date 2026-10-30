using System.Text.Json.Serialization;

namespace PitWall.Contracts.Messages;

/// <summary>
/// Top-level wire message envelope.
/// Every message: { type, seq, t, payload }
/// t = host UTC milliseconds.
/// </summary>
public sealed class WireMessage<T>
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("seq")]
    public long Seq { get; init; }

    /// <summary>Host UTC milliseconds.</summary>
    [JsonPropertyName("t")]
    public long Timestamp { get; init; }

    [JsonPropertyName("payload")]
    public required T Payload { get; init; }
}

/// <summary>
/// Non-generic wire message for deserialization when type is unknown.
/// </summary>
public sealed class WireMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("seq")]
    public long Seq { get; init; }

    [JsonPropertyName("t")]
    public long Timestamp { get; init; }

    [JsonPropertyName("payload")]
    public System.Text.Json.JsonElement Payload { get; init; }
}

/// <summary>
/// Known wire message type strings.
/// </summary>
public static class MessageTypes
{
    // Host → Client
    public const string Live = "live";
    public const string Field = "field";
    public const string Event = "event";
    public const string Call = "call";
    public const string CallStatus = "callStatus";
    public const string RoomStatus = "roomStatus";
    public const string ViewerList = "viewerList";
    public const string Pong = "pong";
    public const string Error = "error";

    // Client → Host / Relay
    public const string Hello = "hello";
    public const string CallMessage = "call";
    public const string CallAck = "callAck";
    public const string Ping = "ping";

    // Relay-specific
    public const string CreateRoom = "createRoom";
    public const string RoomCreated = "roomCreated";
    public const string JoinRequest = "joinRequest";
    public const string JoinApprove = "joinApprove";
    public const string JoinDeny = "joinDeny";
    public const string KickViewer = "kickViewer";
    public const string CloseRoom = "closeRoom";
    public const string RoomClosed = "roomClosed";
}
