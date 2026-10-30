using System.Text.Json.Serialization;
using PitWall.Contracts.Enums;

namespace PitWall.Contracts.Messages;

/// <summary>
/// Payload for the "live" message (10 Hz).
/// Contains the player's telemetry, tyres, and energy data.
/// </summary>
public sealed class LivePayload
{
    [JsonPropertyName("conn")]
    public required ConnectionInfo Conn { get; init; }

    [JsonPropertyName("player")]
    public required PlayerInfo Player { get; init; }

    [JsonPropertyName("driver")]
    public required DriverInfo Driver { get; init; }

    [JsonPropertyName("tyres")]
    public required TyreInfo Tyres { get; init; }

    [JsonPropertyName("energy")]
    public required EnergyInfo Energy { get; init; }
}

public sealed class ConnectionInfo
{
    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConnectionState State { get; init; }

    [JsonPropertyName("pps")]
    public int Pps { get; init; }

    [JsonPropertyName("packetFormat")]
    public int PacketFormat { get; init; }
}

public sealed class PlayerInfo
{
    [JsonPropertyName("carIndex")]
    public int CarIndex { get; init; }

    [JsonPropertyName("position")]
    public int Position { get; init; }

    [JsonPropertyName("lap")]
    public int Lap { get; init; }

    [JsonPropertyName("sector")]
    public int Sector { get; init; }

    [JsonPropertyName("currentLapMs")]
    public float CurrentLapMs { get; init; }

    [JsonPropertyName("lastLapMs")]
    public float LastLapMs { get; init; }

    [JsonPropertyName("bestLapMs")]
    public float BestLapMs { get; init; }

    [JsonPropertyName("gapAheadMs")]
    public float GapAheadMs { get; init; }

    [JsonPropertyName("gapBehindMs")]
    public float GapBehindMs { get; init; }

    [JsonPropertyName("gapLeaderMs")]
    public float GapLeaderMs { get; init; }

    [JsonPropertyName("pitStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PitStatus PitStatus { get; init; }

    [JsonPropertyName("invalidLap")]
    public bool InvalidLap { get; init; }
}

public sealed class DriverInfo
{
    [JsonPropertyName("speedKph")]
    public float SpeedKph { get; init; }

    [JsonPropertyName("gear")]
    public int Gear { get; init; }

    [JsonPropertyName("rpm")]
    public float Rpm { get; init; }

    [JsonPropertyName("maxRpm")]
    public float MaxRpm { get; init; }

    /// <summary>0.0 to 1.0</summary>
    [JsonPropertyName("throttle")]
    public float Throttle { get; init; }

    /// <summary>0.0 to 1.0</summary>
    [JsonPropertyName("brake")]
    public float Brake { get; init; }

    /// <summary>-1.0 (full left) to 1.0 (full right)</summary>
    [JsonPropertyName("steer")]
    public float Steer { get; init; }

    [JsonPropertyName("drs")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DrsState Drs { get; init; }
}

public sealed class TyreInfo
{
    /// <summary>Surface temperature °C [RL, RR, FL, FR]</summary>
    [JsonPropertyName("surfaceC")]
    public required float[] SurfaceC { get; init; }

    /// <summary>Inner temperature °C [RL, RR, FL, FR]</summary>
    [JsonPropertyName("innerC")]
    public required float[] InnerC { get; init; }

    /// <summary>Pressure PSI [RL, RR, FL, FR]</summary>
    [JsonPropertyName("pressurePsi")]
    public required float[] PressurePsi { get; init; }

    /// <summary>Brake temperature °C [RL, RR, FL, FR]</summary>
    [JsonPropertyName("brakeC")]
    public required float[] BrakeC { get; init; }

    /// <summary>Wear percentage [RL, RR, FL, FR]</summary>
    [JsonPropertyName("wearPct")]
    public required float[] WearPct { get; init; }
}

public sealed class EnergyInfo
{
    [JsonPropertyName("fuelKg")]
    public float FuelKg { get; init; }

    [JsonPropertyName("fuelLaps")]
    public float FuelLaps { get; init; }

    [JsonPropertyName("fuelMix")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FuelMix FuelMix { get; init; }

    /// <summary>ERS store as percentage 0-100.</summary>
    [JsonPropertyName("ersPct")]
    public float ErsPct { get; init; }

    [JsonPropertyName("ersMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ErsMode ErsMode { get; init; }

    [JsonPropertyName("ersHarvestedMJ")]
    public float ErsHarvestedMJ { get; init; }

    [JsonPropertyName("ersDeployedMJ")]
    public float ErsDeployedMJ { get; init; }
}
