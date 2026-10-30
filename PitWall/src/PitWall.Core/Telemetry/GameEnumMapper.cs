using F1Game.UDP.Events;
using PitWall.Contracts.Enums;
using GameActualCompound = F1Game.UDP.Enums.ActualCompound;
using GameErsDeployMode = F1Game.UDP.Enums.ErsDeployMode;
using GameEventType = F1Game.UDP.Events.EventType;
using GameFiaFlag = F1Game.UDP.Enums.FiaFlag;
using GameFuelMixOptions = F1Game.UDP.Enums.FuelMixOptions;
using GamePitStatus = F1Game.UDP.Enums.PitStatus;
using GameSafetyCarType = F1Game.UDP.Enums.SafetyCarType;
using GameSessionType = F1Game.UDP.Enums.SessionType;
using GameWeather = F1Game.UDP.Enums.Weather;
using GameDriverStatus = F1Game.UDP.Enums.DriverStatus;
using GameResultStatus = F1Game.UDP.Enums.ResultStatus;
using GameVisualCompound = F1Game.UDP.Enums.VisualCompound;

namespace PitWall.Core.Telemetry;

internal static class GameEnumMapper
{
    public static SessionType Session(GameSessionType value)
        => Enum.IsDefined(value) ? (SessionType)(int)value : SessionType.Unknown;

    public static Weather Weather(GameWeather value)
        => Enum.IsDefined(value) ? (Weather)(int)value : PitWall.Contracts.Enums.Weather.Clear;

    public static SafetyCarStatus SafetyCar(GameSafetyCarType value) => value switch
    {
        GameSafetyCarType.FullSafetyCar => SafetyCarStatus.SC,
        GameSafetyCarType.VirtualSafetyCar => SafetyCarStatus.VSC,
        GameSafetyCarType.FormationLap => SafetyCarStatus.Formation,
        _ => SafetyCarStatus.None
    };

    public static PitStatus Pit(GamePitStatus value) => value switch
    {
        GamePitStatus.Pitting => PitStatus.Pitting,
        GamePitStatus.InPitArea => PitStatus.InPitArea,
        _ => PitStatus.None
    };

    public static DriverStatus Driver(GameDriverStatus value)
        => Enum.IsDefined(value) ? (DriverStatus)(int)value : DriverStatus.InGarage;

    public static ResultStatus Result(GameResultStatus value)
        => Enum.IsDefined(value) ? (ResultStatus)(int)value : ResultStatus.Invalid;

    public static FiaFlag Flag(GameFiaFlag value) => value switch
    {
        GameFiaFlag.Green => FiaFlag.Green,
        GameFiaFlag.Blue => FiaFlag.Blue,
        GameFiaFlag.Yellow => FiaFlag.Yellow,
        _ => FiaFlag.None
    };

    public static FuelMix Fuel(GameFuelMixOptions value)
        => Enum.IsDefined(value) ? (FuelMix)(int)value : FuelMix.Standard;

    public static ErsMode Ers(GameErsDeployMode value)
        => Enum.IsDefined(value) ? (ErsMode)(int)value : ErsMode.None;

    public static TyreCompound Visual(GameVisualCompound value) => value switch
    {
        GameVisualCompound.F1Soft => TyreCompound.Soft,
        GameVisualCompound.F1Medium => TyreCompound.Medium,
        GameVisualCompound.F1Hard => TyreCompound.Hard,
        GameVisualCompound.F1Inter => TyreCompound.Inter,
        GameVisualCompound.F1Wet => TyreCompound.Wet,
        _ => TyreCompound.Unknown
    };

    public static TyreCompound Actual(GameActualCompound value) => value switch
    {
        GameActualCompound.F1C6 or GameActualCompound.F1C5 or GameActualCompound.F1C4 => TyreCompound.Soft,
        GameActualCompound.F1C3 => TyreCompound.Medium,
        GameActualCompound.F1C2 or GameActualCompound.F1C1 or GameActualCompound.F1C0 => TyreCompound.Hard,
        GameActualCompound.F1Inter => TyreCompound.Inter,
        GameActualCompound.F1Wet => TyreCompound.Wet,
        _ => TyreCompound.Unknown
    };

    public static string EventCode(GameEventType type) => type switch
    {
        GameEventType.SessionStarted => "SSTA",
        GameEventType.SessionEnded => "SEND",
        GameEventType.FastestLap => "FTLP",
        GameEventType.Retirement => "RTMT",
        GameEventType.DRSEnabled => "DRSE",
        GameEventType.DRSDisabled => "DRSD",
        GameEventType.TeamMateInPits => "TMPT",
        GameEventType.ChequeredFlag => "CHQF",
        GameEventType.RaceWinner => "RCWN",
        GameEventType.PenaltyIssued => "PENA",
        GameEventType.SpeedTrapTriggered => "SPTP",
        GameEventType.StartLights => "STLG",
        GameEventType.LightsOut => "LGOT",
        GameEventType.DriveThroughServed => "DTSV",
        GameEventType.StopGoServed => "SGSV",
        GameEventType.Flashback => "FLBK",
        GameEventType.ButtonStatus => "BUTN",
        GameEventType.RedFlag => "RDFL",
        GameEventType.Overtake => "OVTK",
        GameEventType.SafetyCar => "SCAR",
        GameEventType.Collision => "COLL",
        _ => "UNKN"
    };
}
