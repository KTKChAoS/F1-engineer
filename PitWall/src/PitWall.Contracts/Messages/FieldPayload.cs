using System.Text.Json.Serialization;
using PitWall.Contracts.Enums;

namespace PitWall.Contracts.Messages;

/// <summary>
/// Payload for the "field" message (2 Hz).
/// Contains session state, all cars, tyre/damage info, lap log, strategy, derived metrics, events.
/// </summary>
public sealed class FieldPayload
{
    [JsonPropertyName("session")]
    public required SessionInfo Session { get; init; }

    [JsonPropertyName("forecast")]
    public required ForecastSample[] Forecast { get; init; }

    [JsonPropertyName("trackPath")]
    public required MapPoint[] TrackPath { get; init; }

    [JsonPropertyName("positionHistory")]
    public required PositionHistoryRecord[] PositionHistory { get; init; }

    [JsonPropertyName("gapHistory")]
    public required GapHistoryRecord[] GapHistory { get; init; }

    [JsonPropertyName("setup")]
    public CarSetupInfo? Setup { get; init; }

    [JsonPropertyName("cars")]
    public required CarInfo[] Cars { get; init; }

    [JsonPropertyName("tyreInfo")]
    public required TyreDetailInfo TyreInfo { get; init; }

    [JsonPropertyName("damage")]
    public required DamageInfo Damage { get; init; }

    [JsonPropertyName("laps")]
    public required LapRecord[] Laps { get; init; }

    [JsonPropertyName("strategy")]
    public required StrategyInfo Strategy { get; init; }

    [JsonPropertyName("derived")]
    public required DerivedMetrics Derived { get; init; }

    [JsonPropertyName("events")]
    public required EventRecord[] Events { get; init; }
}

// ── Session ──────────────────────────────────────────────────────────

public sealed class SessionInfo
{
    [JsonPropertyName("uid")]
    public long Uid { get; init; }

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SessionType Type { get; init; }

    [JsonPropertyName("track")]
    public string Track { get; init; } = "";

    [JsonPropertyName("trackLengthM")]
    public float TrackLengthM { get; init; }

    [JsonPropertyName("totalLaps")]
    public int TotalLaps { get; init; }

    [JsonPropertyName("currentLap")]
    public int CurrentLap { get; init; }

    [JsonPropertyName("timeLeftS")]
    public float TimeLeftS { get; init; }

    [JsonPropertyName("weather")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Weather Weather { get; init; }

    [JsonPropertyName("trackTempC")]
    public float TrackTempC { get; init; }

    [JsonPropertyName("airTempC")]
    public float AirTempC { get; init; }

    [JsonPropertyName("safetyCar")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SafetyCarStatus SafetyCar { get; init; }

    [JsonPropertyName("flag")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FiaFlag Flag { get; init; }

    [JsonPropertyName("pitWindow")]
    public required PitWindowInfo PitWindow { get; init; }

    [JsonPropertyName("paused")]
    public bool Paused { get; init; }
}

public sealed class PitWindowInfo
{
    [JsonPropertyName("ideal")]
    public int Ideal { get; init; }

    [JsonPropertyName("latest")]
    public int Latest { get; init; }
}

// ── Forecast ─────────────────────────────────────────────────────────

public sealed class ForecastSample
{
    [JsonPropertyName("offsetMin")]
    public int OffsetMin { get; init; }

    [JsonPropertyName("weather")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Weather Weather { get; init; }

    [JsonPropertyName("trackTempC")]
    public float TrackTempC { get; init; }

    [JsonPropertyName("airTempC")]
    public float AirTempC { get; init; }

    [JsonPropertyName("rainPct")]
    public int RainPct { get; init; }
}

public sealed class MapPoint
{
    [JsonPropertyName("x")]
    public float X { get; init; }

    [JsonPropertyName("z")]
    public float Z { get; init; }
}

public sealed class PositionHistoryRecord
{
    [JsonPropertyName("lapNo")]
    public int LapNo { get; init; }

    [JsonPropertyName("positions")]
    public required int[] Positions { get; init; }
}

public sealed class GapHistoryRecord
{
    [JsonPropertyName("lapNo")]
    public int LapNo { get; init; }
    [JsonPropertyName("aheadMs")]
    public float AheadMs { get; init; }
    [JsonPropertyName("behindMs")]
    public float? BehindMs { get; init; }
}

public sealed class CarSetupInfo
{
    [JsonPropertyName("frontWing")]
    public float FrontWing { get; init; }
    [JsonPropertyName("rearWing")]
    public float RearWing { get; init; }
    [JsonPropertyName("onThrottle")]
    public float OnThrottle { get; init; }
    [JsonPropertyName("offThrottle")]
    public float OffThrottle { get; init; }
    [JsonPropertyName("frontCamber")]
    public float FrontCamber { get; init; }
    [JsonPropertyName("rearCamber")]
    public float RearCamber { get; init; }
    [JsonPropertyName("frontToe")]
    public float FrontToe { get; init; }
    [JsonPropertyName("rearToe")]
    public float RearToe { get; init; }
    [JsonPropertyName("frontSuspension")]
    public int FrontSuspension { get; init; }
    [JsonPropertyName("rearSuspension")]
    public int RearSuspension { get; init; }
    [JsonPropertyName("frontAntiRollBar")]
    public int FrontAntiRollBar { get; init; }
    [JsonPropertyName("rearAntiRollBar")]
    public int RearAntiRollBar { get; init; }
    [JsonPropertyName("frontSuspensionHeight")]
    public int FrontSuspensionHeight { get; init; }
    [JsonPropertyName("rearSuspensionHeight")]
    public int RearSuspensionHeight { get; init; }
    [JsonPropertyName("brakePressure")]
    public float BrakePressure { get; init; }
    [JsonPropertyName("brakeBias")]
    public float BrakeBias { get; init; }
    [JsonPropertyName("engineBraking")]
    public float EngineBraking { get; init; }
    [JsonPropertyName("tyrePressures")]
    public required float[] TyrePressures { get; init; }
    [JsonPropertyName("ballast")]
    public int Ballast { get; init; }
    [JsonPropertyName("fuelLoad")]
    public float FuelLoad { get; init; }
}

// ── Cars ─────────────────────────────────────────────────────────────

public sealed class CarInfo
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("teamId")]
    public int TeamId { get; init; }

    [JsonPropertyName("teamColor")]
    public string TeamColor { get; init; } = "";

    [JsonPropertyName("raceNumber")]
    public int RaceNumber { get; init; }

    [JsonPropertyName("position")]
    public int Position { get; init; }

    [JsonPropertyName("gridPosition")]
    public int GridPosition { get; init; }

    [JsonPropertyName("gapLeaderMs")]
    public float GapLeaderMs { get; init; }

    [JsonPropertyName("intervalMs")]
    public float IntervalMs { get; init; }

    [JsonPropertyName("lastLapMs")]
    public float LastLapMs { get; init; }

    [JsonPropertyName("bestLapMs")]
    public float BestLapMs { get; init; }

    [JsonPropertyName("compound")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TyreCompound Compound { get; init; }

    [JsonPropertyName("tyreAgeLaps")]
    public int TyreAgeLaps { get; init; }

    [JsonPropertyName("pitStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PitStatus PitStatus { get; init; }

    [JsonPropertyName("stops")]
    public int Stops { get; init; }

    [JsonPropertyName("penaltiesS")]
    public float PenaltiesS { get; init; }

    [JsonPropertyName("driverStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DriverStatus DriverStatus { get; init; }

    [JsonPropertyName("resultStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResultStatus ResultStatus { get; init; }

    [JsonPropertyName("isPlayer")]
    public bool IsPlayer { get; init; }

    [JsonPropertyName("isAi")]
    public bool IsAi { get; init; }
}

// ── Tyre detail (player) ─────────────────────────────────────────────

public sealed class TyreDetailInfo
{
    [JsonPropertyName("compoundActual")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TyreCompound CompoundActual { get; init; }

    [JsonPropertyName("compoundVisual")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TyreCompound CompoundVisual { get; init; }

    [JsonPropertyName("ageLaps")]
    public int AgeLaps { get; init; }

    /// <summary>Damage percentage [RL, RR, FL, FR]</summary>
    [JsonPropertyName("damagePct")]
    public required float[] DamagePct { get; init; }
}

// ── Damage (player) ──────────────────────────────────────────────────

public sealed class DamageInfo
{
    [JsonPropertyName("frontWingL")]
    public float FrontWingL { get; init; }

    [JsonPropertyName("frontWingR")]
    public float FrontWingR { get; init; }

    [JsonPropertyName("rearWing")]
    public float RearWing { get; init; }

    [JsonPropertyName("floor")]
    public float Floor { get; init; }

    [JsonPropertyName("diffuser")]
    public float Diffuser { get; init; }

    [JsonPropertyName("sidepod")]
    public float Sidepod { get; init; }

    [JsonPropertyName("drsFault")]
    public bool DrsFault { get; init; }

    [JsonPropertyName("ersFault")]
    public bool ErsFault { get; init; }

    [JsonPropertyName("gearbox")]
    public float Gearbox { get; init; }

    [JsonPropertyName("engine")]
    public float Engine { get; init; }

    [JsonPropertyName("engineMguh")]
    public float EngineMguh { get; init; }

    [JsonPropertyName("engineEs")]
    public float EngineEs { get; init; }

    [JsonPropertyName("engineCe")]
    public float EngineCe { get; init; }

    [JsonPropertyName("engineIce")]
    public float EngineIce { get; init; }

    [JsonPropertyName("engineMguk")]
    public float EngineMguk { get; init; }

    [JsonPropertyName("engineTc")]
    public float EngineTc { get; init; }

    [JsonPropertyName("engineBlown")]
    public bool EngineBlown { get; init; }

    [JsonPropertyName("engineSeized")]
    public bool EngineSeized { get; init; }
}

// ── Lap records (player history) ─────────────────────────────────────

public sealed class LapRecord
{
    [JsonPropertyName("lapNo")]
    public int LapNo { get; init; }

    [JsonPropertyName("lapMs")]
    public float LapMs { get; init; }

    [JsonPropertyName("s1Ms")]
    public float S1Ms { get; init; }

    [JsonPropertyName("s2Ms")]
    public float S2Ms { get; init; }

    [JsonPropertyName("s3Ms")]
    public float S3Ms { get; init; }

    [JsonPropertyName("valid")]
    public bool Valid { get; init; }

    [JsonPropertyName("compound")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TyreCompound Compound { get; init; }

    /// <summary>Tyre wear at lap end [RL, RR, FL, FR]</summary>
    [JsonPropertyName("wearPct")]
    public required float[] WearPct { get; init; }

    [JsonPropertyName("fuelKg")]
    public float FuelKg { get; init; }
}

// ── Strategy ─────────────────────────────────────────────────────────

public sealed class StrategyInfo
{
    [JsonPropertyName("stints")]
    public required StintInfo[] Stints { get; init; }

    [JsonPropertyName("tyreSets")]
    public required TyreSetInfo[] TyreSets { get; init; }
}

public sealed class StintInfo
{
    [JsonPropertyName("startLap")]
    public int StartLap { get; init; }

    [JsonPropertyName("endLap")]
    public int EndLap { get; init; }

    [JsonPropertyName("compound")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TyreCompound Compound { get; init; }
}

public sealed class TyreSetInfo
{
    [JsonPropertyName("compound")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TyreCompound Compound { get; init; }

    [JsonPropertyName("wearPct")]
    public float WearPct { get; init; }

    [JsonPropertyName("lifeLeft")]
    public float LifeLeft { get; init; }

    [JsonPropertyName("available")]
    public bool Available { get; init; }

    [JsonPropertyName("fitted")]
    public bool Fitted { get; init; }
}

// ── Derived metrics ──────────────────────────────────────────────────

public sealed class DerivedMetrics
{
    [JsonPropertyName("lapsRemaining")]
    public float LapsRemaining { get; init; }

    /// <summary>Wear rate per wheel per lap [RL, RR, FL, FR]. Null if insufficient data.</summary>
    [JsonPropertyName("tyreWearRate")]
    public float[]? TyreWearRate { get; init; }

    /// <summary>Predicted laps to threshold per wheel [RL, RR, FL, FR]. Null if insufficient data.</summary>
    [JsonPropertyName("tyreLapsToThreshold")]
    public float[]? TyreLapsToThreshold { get; init; }

    [JsonPropertyName("fuelBurnPerLap")]
    public float? FuelBurnPerLap { get; init; }

    /// <summary>Positive = surplus laps, negative = deficit.</summary>
    [JsonPropertyName("fuelSurplusLaps")]
    public float? FuelSurplusLaps { get; init; }

    /// <summary>Gap trend per lap (negative = closing).</summary>
    [JsonPropertyName("gapAheadTrend")]
    public float? GapAheadTrend { get; init; }

    [JsonPropertyName("gapBehindTrend")]
    public float? GapBehindTrend { get; init; }

    [JsonPropertyName("gapBehindUnder1sLaps")]
    public int GapBehindUnder1sLaps { get; init; }

    [JsonPropertyName("pitLossS")]
    public float PitLossS { get; init; }

    [JsonPropertyName("undercutHint")]
    public bool UndercutHint { get; init; }
}

// ── Event records ────────────────────────────────────────────────────

public sealed class EventRecord
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = "";

    [JsonPropertyName("lap")]
    public int Lap { get; init; }

    [JsonPropertyName("carIndex")]
    public int CarIndex { get; init; }

    [JsonPropertyName("otherCarIndex")]
    public int OtherCarIndex { get; init; } = -1;

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";
}
