namespace PitWall.Contracts.Enums;

/// <summary>
/// Connection state of the UDP receiver.
/// </summary>
public enum ConnectionState
{
    Waiting,
    Receiving,
    Stale
}

/// <summary>
/// DRS state for the player car.
/// </summary>
public enum DrsState
{
    Off,
    Available,
    Active
}

/// <summary>
/// Pit status of a car.
/// </summary>
public enum PitStatus
{
    None,
    Pitting,
    InPitArea
}

/// <summary>
/// Safety car status.
/// </summary>
public enum SafetyCarStatus
{
    None,
    SC,
    VSC,
    Formation
}

/// <summary>
/// Weather condition.
/// </summary>
public enum Weather
{
    Clear,
    LightCloud,
    Overcast,
    LightRain,
    HeavyRain,
    Storm
}

/// <summary>
/// Session type from the game. Numeric values match F1 25 UDP / F1Game.UDP.
/// </summary>
public enum SessionType
{
    Unknown = 0,
    Practice1 = 1,
    Practice2 = 2,
    Practice3 = 3,
    ShortPractice = 4,
    Qualifying1 = 5,
    Qualifying2 = 6,
    Qualifying3 = 7,
    ShortQualifying = 8,
    OneShotQualifying = 9,
    SprintShootout1 = 10,
    SprintShootout2 = 11,
    SprintShootout3 = 12,
    ShortSprintShootout = 13,
    OneShotSprintShootout = 14,
    Race = 15,
    Race2 = 16,
    Race3 = 17,
    TimeTrial = 18
}

/// <summary>
/// FIA flag displayed to a car.
/// </summary>
public enum FiaFlag
{
    None,
    Green,
    Blue,
    Yellow
}

/// <summary>
/// Driver status on track.
/// </summary>
public enum DriverStatus
{
    InGarage,
    FlyingLap,
    InLap,
    OutLap,
    OnTrack
}

/// <summary>
/// Result status for a car.
/// </summary>
public enum ResultStatus
{
    Invalid,
    Inactive,
    Active,
    Finished,
    DidNotFinish,
    Disqualified,
    NotClassified,
    Retired
}

/// <summary>
/// Fuel mix mode.
/// </summary>
public enum FuelMix
{
    Lean,
    Standard,
    Rich,
    Max
}

/// <summary>
/// ERS deploy mode.
/// </summary>
public enum ErsMode
{
    None,
    Medium,
    Hotlap,
    Overtake
}

/// <summary>
/// Tyre compound as reported by the game (visual representation).
/// </summary>
public enum TyreCompound
{
    Unknown,
    Soft,
    Medium,
    Hard,
    Inter,
    Wet
}

/// <summary>
/// Client role for WebSocket connections.
/// </summary>
public enum ClientRole
{
    Engineer,
    Driver
}

/// <summary>
/// Call preset identifiers.
/// </summary>
public enum CallPreset
{
    BoxThisLap,
    BoxNextLap,
    StayOut,
    PushNow,
    ManageTyres,
    SaveFuel,
    MaxErs,
    CarBehindClosing,
    GoodLap,
    FreeText
}

/// <summary>
/// Status of a call through the system.
/// </summary>
public enum CallStatus
{
    Sent,
    Delivered,
    Acknowledged
}
