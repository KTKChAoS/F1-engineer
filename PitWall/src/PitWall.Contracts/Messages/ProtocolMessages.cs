using System.Text.Json.Serialization;
using PitWall.Contracts.Enums;

namespace PitWall.Contracts.Messages;

// ── Client → Host / Relay ────────────────────────────────────────────

public sealed class HelloMessage
{
    [JsonPropertyName("role")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClientRole Role { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("clientVersion")]
    public string ClientVersion { get; init; } = "";
}

public sealed class CallMessage
{
    [JsonPropertyName("callId")]
    public string CallId { get; init; } = "";

    [JsonPropertyName("preset")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CallPreset Preset { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}

public sealed class CallAckMessage
{
    [JsonPropertyName("callId")]
    public string CallId { get; init; } = "";
}

public sealed class PingMessage
{
    [JsonPropertyName("ts")]
    public long Ts { get; init; }
}

// ── Host → Client ────────────────────────────────────────────────────

public sealed class CallStatusMessage
{
    [JsonPropertyName("callId")]
    public string CallId { get; init; } = "";

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CallStatus Status { get; init; }
}

public sealed class PongMessage
{
    [JsonPropertyName("ts")]
    public long Ts { get; init; }

    [JsonPropertyName("serverTs")]
    public long ServerTs { get; init; }
}

public sealed class ErrorMessage
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
}

// ── Relay-specific ───────────────────────────────────────────────────

public sealed class CreateRoomMessage
{
    [JsonPropertyName("clientVersion")]
    public string ClientVersion { get; init; } = "";

    [JsonPropertyName("hostName")]
    public string HostName { get; init; } = "";
}

public sealed class RoomCreatedMessage
{
    [JsonPropertyName("roomCode")]
    public string RoomCode { get; init; } = "";

    [JsonPropertyName("hostSecret")]
    public string HostSecret { get; init; } = "";

    [JsonPropertyName("joinUrl")]
    public string JoinUrl { get; init; } = "";

    [JsonPropertyName("expiresAt")]
    public long ExpiresAt { get; init; }
}

public sealed class JoinRequestMessage
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

public sealed class JoinApproveMessage
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = "";
}

public sealed class JoinDenyMessage
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = "";
}

public sealed class KickViewerMessage
{
    [JsonPropertyName("viewerId")]
    public string ViewerId { get; init; } = "";
}

public sealed class RoomStatusMessage
{
    [JsonPropertyName("roomCode")]
    public string RoomCode { get; init; } = "";

    [JsonPropertyName("joinUrl")]
    public string JoinUrl { get; init; } = "";

    [JsonPropertyName("viewerCount")]
    public int ViewerCount { get; init; }

    [JsonPropertyName("approvalRequired")]
    public bool ApprovalRequired { get; init; }

    [JsonPropertyName("callsAllowed")]
    public bool CallsAllowed { get; init; }
}

public sealed class ViewerListMessage
{
    [JsonPropertyName("viewers")]
    public required ViewerInfo[] Viewers { get; init; }
}

public sealed class ViewerInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("role")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClientRole Role { get; init; }

    [JsonPropertyName("connected")]
    public bool Connected { get; init; }
}

public sealed class RoomClosedMessage
{
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = "";
}
