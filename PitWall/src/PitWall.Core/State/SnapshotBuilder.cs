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
        Forecast = snapshot.Forecast.ToArray(),
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
        Derived = BuildDerived(snapshot),
        Events = snapshot.Events.ToArray()
    };

    private static DerivedMetrics BuildDerived(RaceSnapshot snapshot)
    {
        var lapsRemaining = Math.Max(0, snapshot.TotalLaps - snapshot.Player.Lap + 1);
        var tyreRates = new float[Constants.WheelCount];
        var tyreLaps = new float[Constants.WheelCount];
        var tyreRateAvailable = false;
        var tyreHistory = snapshot.LapLog.TakeLast(4).Reverse().TakeWhile(lap => lap.Compound == snapshot.VisualCompound).Reverse().ToArray();
        if (tyreHistory.Length >= 2)
        {
            for (var wheel = 0; wheel < Constants.WheelCount; wheel++)
            {
                var rates = new List<float>();
                for (var i = 1; i < tyreHistory.Length; i++)
                {
                    var lapSpan = tyreHistory[i].LapNo - tyreHistory[i - 1].LapNo;
                    if (lapSpan > 0 && wheel < tyreHistory[i].WearPct.Length && wheel < tyreHistory[i - 1].WearPct.Length)
                        rates.Add(Math.Max(0, tyreHistory[i].WearPct[wheel] - tyreHistory[i - 1].WearPct[wheel]) / lapSpan);
                }
                if (rates.Count == 0)
                    continue;
                var rate = rates.Average();
                tyreRates[wheel] = rate;
                tyreLaps[wheel] = rate > 0 ? Math.Max(0, (Constants.DefaultTyreWearThresholdPct - snapshot.Tyres.WearPct[wheel]) / rate) : 0;
                tyreRateAvailable = true;
            }
        }

        var fuelSamples = snapshot.LapLog.Where(lap => lap.LapNo > 1).TakeLast(4).ToArray();
        var fuelBurns = new List<float>();
        for (var i = 1; i < fuelSamples.Length; i++)
        {
            var lapSpan = fuelSamples[i].LapNo - fuelSamples[i - 1].LapNo;
            var burn = fuelSamples[i - 1].FuelKg - fuelSamples[i].FuelKg;
            if (lapSpan > 0 && burn > 0)
                fuelBurns.Add(burn / lapSpan);
        }
        float? fuelBurn = fuelBurns.Count == 0 ? null : fuelBurns.Average();
        float? fuelSurplus = fuelBurn is > 0 ? snapshot.Energy.FuelKg / fuelBurn.Value - lapsRemaining : null;
        var ahead = snapshot.FieldCars.FirstOrDefault(car => car.Position == snapshot.Player.Position - 1);
        var undercut = ahead is not null
            && snapshot.Player.GapAheadMs < Constants.UndercutGapThresholdSeconds * 1000
            && ahead.TyreAgeLaps >= snapshot.TyreAgeLaps + Constants.UndercutTyreAgeDeltaLaps;

        return new DerivedMetrics
        {
            LapsRemaining = lapsRemaining,
            TyreWearRate = tyreRateAvailable ? tyreRates : null,
            TyreLapsToThreshold = tyreRateAvailable ? tyreLaps : null,
            FuelBurnPerLap = fuelBurn,
            FuelSurplusLaps = fuelSurplus,
            GapAheadTrend = snapshot.GapAheadTrendMsPerLap / 1000f,
            GapBehindTrend = snapshot.GapBehindTrendMsPerLap / 1000f,
            GapBehindUnder1sLaps = snapshot.GapBehindUnder1sLaps,
            PitLossS = Constants.DefaultPitLossSeconds,
            UndercutHint = undercut
        };
    }
}
