using PitWall.Contracts;
using PitWall.Contracts.Messages;

namespace PitWall.Core.State;

/// <summary>Projects the decoder-owned snapshot into the public WebSocket payloads.</summary>
public static class SnapshotBuilder
{
    public static IReadOnlyList<EventRecord> BuildEvents(RaceSnapshot snapshot) => snapshot.Events;

    public static LivePayload BuildLive(RaceSnapshot snapshot) => new()
    {
        Conn = new ConnectionInfo
        {
            State = snapshot.Connection,
            Pps = snapshot.Pps,
            PacketFormat = snapshot.PacketFormat
        },
        Player = snapshot.Player,
        Driver = snapshot.Driver,
        Tyres = snapshot.Tyres,
        Energy = snapshot.Energy
    };

    public static FieldPayload BuildField(RaceSnapshot snapshot) => new()
    {
        Session = new SessionInfo
        {
            Uid = unchecked((long)snapshot.SessionUid),
            Type = snapshot.SessionType,
            Track = snapshot.Track,
            TrackLengthM = snapshot.TrackLengthM,
            TotalLaps = snapshot.TotalLaps,
            CurrentLap = snapshot.Player.Lap,
            TimeLeftS = snapshot.TimeLeftS,
            Weather = snapshot.Weather,
            TrackTempC = snapshot.TrackTempC,
            AirTempC = snapshot.AirTempC,
            SafetyCar = snapshot.SafetyCar,
            Paused = snapshot.Paused,
            PitWindow = new PitWindowInfo()
        },
        Forecast = [],
        Cars = snapshot.FieldCars.ToArray(),
        TyreInfo = new TyreDetailInfo
        {
            CompoundActual = snapshot.VisualCompound,
            CompoundVisual = snapshot.VisualCompound,
            AgeLaps = snapshot.TyreAgeLaps,
            DamagePct = new float[Constants.WheelCount]
        },
        Damage = snapshot.Damage,
        Laps = snapshot.LapLog.ToArray(),
        Strategy = new StrategyInfo { Stints = [], TyreSets = [] },
        Derived = new DerivedMetrics
        {
            LapsRemaining = Math.Max(0, snapshot.TotalLaps - snapshot.Player.Lap),
            GapAheadTrend = snapshot.GapAheadTrendMsPerLap / 1000f,
            GapBehindTrend = snapshot.GapBehindTrendMsPerLap / 1000f,
            PitLossS = Constants.DefaultPitLossSeconds
        },
        Events = snapshot.Events.ToArray()
    };
}
