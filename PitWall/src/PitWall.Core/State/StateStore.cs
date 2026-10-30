using F1Game.UDP.Data;
using F1Game.UDP.Packets;
using PitWall.Contracts;
using PitWall.Contracts.Enums;
using PitWall.Contracts.Messages;
using PitWall.Core.Telemetry;

namespace PitWall.Core.State;

public sealed class StateStore
{
    private readonly object _gate = new();
    private readonly Queue<DateTimeOffset> _packetTimes = new();
    private readonly List<EventRecord> _events = [];
    private readonly List<LapRecord> _lapLog = [];
    private readonly List<GapSample> _gapSamples = [];
    private readonly CarScratch[] _cars = new CarScratch[Constants.MaxCars];
    private RaceSnapshot _published = RaceSnapshot.Empty;
    private Mutable _m = new();
    private int _nextEventId = 1;

    public RaceSnapshot Current => Volatile.Read(ref _published)!;

    public void Reset()
    {
        lock (_gate)
        {
            _m = new Mutable { LastPacketUtc = _m.LastPacketUtc, PacketFormat = _m.PacketFormat };
            Array.Clear(_cars);
            _events.Clear();
            _lapLog.Clear();
            _gapSamples.Clear();
            _nextEventId = 1;
            PublishLocked(DateTimeOffset.UtcNow);
        }
    }

    public void NoteStats(long received, long dropped, long malformed)
    {
        lock (_gate)
        {
            _m.Received = received;
            _m.Dropped = dropped;
            _m.Malformed = malformed;
        }
    }

    public void Apply(UnionPacket packet, DateTimeOffset now)
    {
        lock (_gate)
        {
            var header = packet.Header;
            if (_m.SessionUid != 0 && header.SessionUID != _m.SessionUid)
            {
                _m = new Mutable();
                Array.Clear(_cars);
                _events.Clear();
                _lapLog.Clear();
                _gapSamples.Clear();
                _nextEventId = 1;
            }

            _m.SessionUid = header.SessionUID;
            _m.PacketFormat = header.PacketFormat;
            _m.WrongFormat = header.PacketFormat != Constants.PacketFormat2025;
            _m.PlayerIndex = Math.Clamp((int)header.PlayerCarIndex, 0, Constants.MaxCars - 1);
            _m.LastPacketUtc = now;
            _packetTimes.Enqueue(now);
            while (_packetTimes.Count > 0 && (now - _packetTimes.Peek()).TotalSeconds > 1)
            {
                _packetTimes.Dequeue();
            }

            if (packet.TryGetSessionDataPacket(out var session))
            {
                ApplySession(session);
            }
            else if (packet.TryGetLapDataPacket(out var laps))
            {
                ApplyLaps(laps);
            }
            else if (packet.TryGetCarTelemetryDataPacket(out var telemetry))
            {
                ApplyTelemetry(telemetry);
            }
            else if (packet.TryGetCarStatusDataPacket(out var status))
            {
                ApplyStatus(status);
            }
            else if (packet.TryGetCarDamageDataPacket(out var damage))
            {
                ApplyDamage(damage);
            }
            else if (packet.TryGetParticipantsDataPacket(out var participants))
            {
                ApplyParticipants(participants);
            }
            else if (packet.TryGetEventDataPacket(out var evt))
            {
                ApplyEvent(evt);
            }

            PublishLocked(now);
        }
    }

    private void ApplySession(SessionDataPacket session)
    {
        _m.SessionType = GameEnumMapper.Session(session.SessionType);
        _m.TrackId = (int)session.Track;
        _m.Track = TrackData.GetTrackName(_m.TrackId);
        _m.TrackLengthM = session.TrackLength;
        _m.TotalLaps = session.TotalLaps;
        _m.TimeLeftS = session.SessionTimeLeft;
        _m.Weather = GameEnumMapper.Weather(session.Weather);
        _m.TrackTempC = session.TrackTemperature;
        _m.AirTempC = session.AirTemperature;
        _m.SafetyCar = GameEnumMapper.SafetyCar(session.SafetyCarStatus);
        _m.Paused = session.GamePaused;
    }

    private void ApplyLaps(LapDataPacket packet)
    {
        var laps = packet.LapData.AsSpan();
        var n = Math.Min(laps.Length, Constants.MaxCars);
        var oldPlayerLap = _cars[_m.PlayerIndex].Lap;
        for (var i = 0; i < n; i++)
        {
            ref var dest = ref _cars[i];
            var src = laps[i];
            if (i == _m.PlayerIndex && dest.Lap > 0 && src.CurrentLapNum > dest.Lap && src.LastLapTimeInMS > 0)
            {
                _lapLog.Add(new LapRecord
                {
                    LapNo = dest.Lap,
                    LapMs = src.LastLapTimeInMS,
                    S1Ms = ToMs(src.Sector1TimeInMinutes, src.Sector1TimeInMS),
                    S2Ms = ToMs(src.Sector2TimeInMinutes, src.Sector2TimeInMS),
                    S3Ms = Math.Max(0, src.LastLapTimeInMS - ToMs(src.Sector1TimeInMinutes, src.Sector1TimeInMS) - ToMs(src.Sector2TimeInMinutes, src.Sector2TimeInMS)),
                    Valid = !dest.Invalid,
                    Compound = dest.VisualCompound,
                    WearPct = dest.WearPct?.ToArray() ?? new float[4],
                    FuelKg = dest.FuelKg
                });
                if (_lapLog.Count > 100)
                    _lapLog.RemoveAt(0);
            }
            dest.LastLapMs = src.LastLapTimeInMS;
            dest.CurrentLapMs = src.CurrentLapTimeInMS;
            dest.Position = src.CarPosition;
            dest.Lap = src.CurrentLapNum;
            dest.Sector = (byte)((byte)src.Sector + 1);
            dest.Pit = GameEnumMapper.Pit(src.PitStatus);
            dest.Invalid = src.IsCurrentLapInvalid;
            dest.GapAheadMs = ToMs(src.DeltaToCarInFrontInMinutes, src.DeltaToCarInFrontInMS);
            dest.GapLeaderMs = ToMs(src.DeltaToRaceLeaderInMinutes, src.DeltaToRaceLeaderInMS);
            dest.Grid = src.GridPosition;
            dest.Stops = src.NumPitStops;
            dest.PenaltiesS = src.Penalties;
            dest.DriverStatus = GameEnumMapper.Driver(src.DriverStatus);
            dest.ResultStatus = GameEnumMapper.Result(src.ResultStatus);
            dest.Sector1Ms = ToMs(src.Sector1TimeInMinutes, src.Sector1TimeInMS);
            dest.Sector2Ms = ToMs(src.Sector2TimeInMinutes, src.Sector2TimeInMS);

            if (src.LastLapTimeInMS > 0 && (dest.BestLapMs == 0 || src.LastLapTimeInMS < dest.BestLapMs))
            {
                dest.BestLapMs = src.LastLapTimeInMS;
            }
        }

        var player = _cars[_m.PlayerIndex];
        if (oldPlayerLap > 0 && player.Lap > oldPlayerLap)
        {
            _gapSamples.Add(new GapSample(player.Lap, player.GapAheadMs, FindGapBehind(player.Position)));
            if (_gapSamples.Count > 4)
                _gapSamples.RemoveAt(0);
        }
    }

    private void ApplyTelemetry(CarTelemetryDataPacket packet)
    {
        var cars = packet.CarTelemetryData.AsSpan();
        var n = Math.Min(cars.Length, Constants.MaxCars);
        for (var i = 0; i < n; i++)
        {
            ref var dest = ref _cars[i];
            var src = cars[i];
            dest.SpeedKph = src.Speed;
            dest.Throttle = src.Throttle;
            dest.Brake = src.Brake;
            dest.Steer = src.Steer;
            dest.Gear = src.Gear;
            dest.Rpm = src.EngineRPM;
            dest.DrsOn = src.IsDrsOn;
            dest.SurfaceC = ToFloats(src.TyresSurfaceTemperature);
            dest.InnerC = ToFloats(src.TyresInnerTemperature);
            dest.PressurePsi = ToFloats(src.TyresPressure);
            dest.BrakeC = ToFloats(src.BrakesTemperature);
        }
    }

    private void ApplyStatus(CarStatusDataPacket packet)
    {
        var cars = packet.CarStatusData.AsSpan();
        var n = Math.Min(cars.Length, Constants.MaxCars);
        for (var i = 0; i < n; i++)
        {
            ref var dest = ref _cars[i];
            var src = cars[i];
            dest.FuelMix = GameEnumMapper.Fuel(src.FuelMix);
            dest.FuelKg = src.FuelInTank;
            dest.FuelLaps = src.FuelRemainingLaps;
            dest.MaxRpm = src.MaxRPM;
            dest.DrsAllowed = src.DrsAllowed;
            dest.ActualCompound = GameEnumMapper.Actual(src.ActualTyreCompound);
            dest.VisualCompound = GameEnumMapper.Visual(src.VisualTyreCompound);
            dest.TyreAgeLaps = src.TyresAgeLaps;
            dest.ErsStoreJ = src.ErsStoreEnergy;
            dest.ErsMode = GameEnumMapper.Ers(src.ErsDeployMode);
            dest.ErsHarvestedJ = src.ErsHarvestedThisLapMGUK + src.ErsHarvestedThisLapMGUH;
            dest.ErsDeployedJ = src.ErsDeployedThisLap;
            dest.Flag = GameEnumMapper.Flag(src.VehicleFiaFlags);
        }
    }

    private void ApplyDamage(CarDamageDataPacket packet)
    {
        var cars = packet.CarDamageData.AsSpan();
        var n = Math.Min(cars.Length, Constants.MaxCars);
        for (var i = 0; i < n; i++)
        {
            ref var dest = ref _cars[i];
            var src = cars[i];
            dest.WearPct = ToFloats(src.TyresWear);
            dest.FrontWingL = src.FrontLeftWingDamage;
            dest.FrontWingR = src.FrontRightWingDamage;
            dest.RearWing = src.RearWingDamage;
            dest.Floor = src.FloorDamage;
            dest.Diffuser = src.DiffuserDamage;
            dest.Sidepod = src.SidepodDamage;
            dest.DrsFault = src.DrsFault;
            dest.ErsFault = src.ErsFault;
            dest.Gearbox = src.GearBoxDamage;
            dest.Engine = src.EngineDamage;
            dest.EngineMguh = src.EngineMGUHWear;
            dest.EngineEs = src.EngineESWear;
            dest.EngineCe = src.EngineCEWear;
            dest.EngineIce = src.EngineICEWear;
            dest.EngineMguk = src.EngineMGUKWear;
            dest.EngineTc = src.EngineTCWear;
            dest.EngineBlown = src.EngineBlown;
            dest.EngineSeized = src.EngineSeized;
        }
    }

    private void ApplyParticipants(ParticipantsDataPacket packet)
    {
        var cars = packet.Participants.AsSpan();
        var n = Math.Min(cars.Length, Constants.MaxCars);
        for (var i = 0; i < n; i++)
        {
            _cars[i].Name = cars[i].Name;
            _cars[i].TeamId = (byte)cars[i].Team;
            _cars[i].RaceNumber = cars[i].RaceNumber;
            _cars[i].IsAi = cars[i].IsAiControlled;
        }
    }

    private void ApplyEvent(EventDataPacket packet)
    {
        var player = _cars[_m.PlayerIndex];
        var record = new EventRecord
        {
            Id = _nextEventId++,
            Code = GameEnumMapper.EventCode(packet.EventDetails.EventType),
            Lap = player.Lap,
            CarIndex = _m.PlayerIndex,
            Text = GameEnumMapper.EventCode(packet.EventDetails.EventType)
        };
        _events.Add(record);
        while (_events.Count > Constants.MaxEventsStored)
        {
            _events.RemoveAt(0);
        }
    }

    private void PublishLocked(DateTimeOffset now)
    {
        var player = _cars[_m.PlayerIndex];
        var gapBehind = FindGapBehind(player.Position) ?? 0f;

        var age = _m.LastPacketUtc is { } last ? now - last : Timeout.InfiniteTimeSpan;
        var connection = age == Timeout.InfiniteTimeSpan
            ? ConnectionState.Waiting
            : age.TotalMilliseconds <= Constants.ConnectionStaleThresholdMs
                ? ConnectionState.Receiving
                : ConnectionState.Stale;

        var snapshot = new RaceSnapshot
        {
            Connection = connection,
            Pps = _packetTimes.Count,
            PacketFormat = _m.PacketFormat,
            WrongPacketFormat = _m.WrongFormat,
            LastPacketUtc = _m.LastPacketUtc,
            LastPacketAge = age,
            Received = _m.Received,
            Dropped = _m.Dropped,
            Malformed = _m.Malformed,
            SessionUid = _m.SessionUid,
            SessionType = _m.SessionType,
            Track = _m.Track,
            TrackId = _m.TrackId,
            TrackLengthM = _m.TrackLengthM,
            TotalLaps = _m.TotalLaps,
            TimeLeftS = _m.TimeLeftS,
            Weather = _m.Weather,
            TrackTempC = _m.TrackTempC,
            AirTempC = _m.AirTempC,
            SafetyCar = _m.SafetyCar,
            Paused = _m.Paused,
            VisualCompound = player.VisualCompound,
            TyreAgeLaps = player.TyreAgeLaps,
            Events = _events.ToArray(),
            LapLog = _lapLog.ToArray(),
            Damage = new DamageInfo
            {
                FrontWingL = player.FrontWingL,
                FrontWingR = player.FrontWingR,
                RearWing = player.RearWing,
                Floor = player.Floor,
                Diffuser = player.Diffuser,
                Sidepod = player.Sidepod,
                DrsFault = player.DrsFault,
                ErsFault = player.ErsFault,
                Gearbox = player.Gearbox,
                Engine = player.Engine,
                EngineMguh = player.EngineMguh,
                EngineEs = player.EngineEs,
                EngineCe = player.EngineCe,
                EngineIce = player.EngineIce,
                EngineMguk = player.EngineMguk,
                EngineTc = player.EngineTc,
                EngineBlown = player.EngineBlown,
                EngineSeized = player.EngineSeized
            },
            GapAheadTrendMsPerLap = CalculateGapTrend(ahead: true),
            GapBehindTrendMsPerLap = CalculateGapTrend(ahead: false),
            FieldCars = _cars.Select((car, i) => new CarInfo
            {
                Index = i,
                Name = car.Name,
                TeamId = car.TeamId,
                RaceNumber = car.RaceNumber,
                Position = car.Position,
                GridPosition = car.Grid,
                GapLeaderMs = car.GapLeaderMs,
                IntervalMs = car.GapAheadMs,
                LastLapMs = car.LastLapMs,
                BestLapMs = car.BestLapMs,
                Compound = car.VisualCompound,
                TyreAgeLaps = car.TyreAgeLaps,
                PitStatus = car.Pit,
                Stops = car.Stops,
                PenaltiesS = car.PenaltiesS,
                DriverStatus = car.DriverStatus,
                ResultStatus = car.ResultStatus,
                IsPlayer = i == _m.PlayerIndex,
                IsAi = car.IsAi
            }).ToArray(),
            Player = new PlayerInfo
            {
                CarIndex = _m.PlayerIndex,
                Position = player.Position,
                Lap = player.Lap,
                Sector = player.Sector,
                CurrentLapMs = player.CurrentLapMs,
                LastLapMs = player.LastLapMs,
                BestLapMs = player.BestLapMs,
                GapAheadMs = player.GapAheadMs,
                GapBehindMs = gapBehind,
                GapLeaderMs = player.GapLeaderMs,
                PitStatus = player.Pit,
                InvalidLap = player.Invalid
            },
            Driver = new DriverInfo
            {
                SpeedKph = player.SpeedKph,
                Gear = player.Gear,
                Rpm = player.Rpm,
                MaxRpm = player.MaxRpm,
                Throttle = player.Throttle,
                Brake = player.Brake,
                Steer = player.Steer,
                Drs = player.DrsOn
                    ? DrsState.Active
                    : player.DrsAllowed
                        ? DrsState.Available
                        : DrsState.Off
            },
            Tyres = new TyreInfo
            {
                SurfaceC = player.SurfaceC ?? new float[4],
                InnerC = player.InnerC ?? new float[4],
                PressurePsi = player.PressurePsi ?? new float[4],
                BrakeC = player.BrakeC ?? new float[4],
                WearPct = player.WearPct ?? new float[4]
            },
            Energy = new EnergyInfo
            {
                FuelKg = player.FuelKg,
                FuelLaps = player.FuelLaps,
                FuelMix = player.FuelMix,
                ErsPct = Constants.ErsMaxStoreMJ <= 0
                    ? 0
                    : Math.Clamp(player.ErsStoreJ / (Constants.ErsMaxStoreMJ * 1_000_000f) * 100f, 0, 100),
                ErsMode = player.ErsMode,
                ErsHarvestedMJ = player.ErsHarvestedJ / 1_000_000f,
                ErsDeployedMJ = player.ErsDeployedJ / 1_000_000f
            }
        };

        Volatile.Write(ref _published, snapshot);
    }

    private static uint ToMs(byte minutes, ushort msPart) => (uint)minutes * 60_000u + msPart;

    private float? FindGapBehind(byte playerPosition)
    {
        for (var i = 0; i < _cars.Length; i++)
        {
            if (_cars[i].Position == playerPosition + 1)
                return _cars[i].GapAheadMs;
        }
        return null;
    }

    private float? CalculateGapTrend(bool ahead)
    {
        if (_gapSamples.Count < 2)
            return null;

        var first = _gapSamples[0];
        var last = _gapSamples[^1];
        var lapSpan = last.Lap - first.Lap;
        if (lapSpan <= 0 || (!ahead && (first.BehindMs is null || last.BehindMs is null)))
            return null;

        var firstGap = ahead ? first.AheadMs : first.BehindMs!.Value;
        var lastGap = ahead ? last.AheadMs : last.BehindMs!.Value;
        return (lastGap - firstGap) / lapSpan;
    }

    private static float[] ToFloats<T>(Tyres<T> tyres) where T : unmanaged
    {
        return
        [
            Convert.ToSingle(tyres.RearLeft),
            Convert.ToSingle(tyres.RearRight),
            Convert.ToSingle(tyres.FrontLeft),
            Convert.ToSingle(tyres.FrontRight)
        ];
    }

    private struct CarScratch
    {
        public CarScratch() => Name = string.Empty;

        public uint LastLapMs;
        public uint CurrentLapMs;
        public uint BestLapMs;
        public byte Position;
        public byte Lap;
        public byte Sector;
        public PitStatus Pit;
        public bool Invalid;
        public float GapAheadMs;
        public float GapLeaderMs;
        public byte Grid;
        public byte Stops;
        public byte PenaltiesS;
        public DriverStatus DriverStatus;
        public ResultStatus ResultStatus;
        public uint Sector1Ms;
        public uint Sector2Ms;
        public float SpeedKph;
        public float Throttle;
        public float Brake;
        public float Steer;
        public int Gear;
        public float Rpm;
        public float MaxRpm;
        public bool DrsOn;
        public bool DrsAllowed;
        public float[]? SurfaceC;
        public float[]? InnerC;
        public float[]? PressurePsi;
        public float[]? BrakeC;
        public float[]? WearPct;
        public FuelMix FuelMix;
        public float FuelKg;
        public float FuelLaps;
        public TyreCompound ActualCompound;
        public TyreCompound VisualCompound;
        public int TyreAgeLaps;
        public float ErsStoreJ;
        public ErsMode ErsMode;
        public float ErsHarvestedJ;
        public float ErsDeployedJ;
        public FiaFlag Flag;
        public string Name = "";
        public byte TeamId;
        public byte RaceNumber;
        public bool IsAi;
        public float FrontWingL, FrontWingR, RearWing, Floor, Diffuser, Sidepod;
        public bool DrsFault, ErsFault, EngineBlown, EngineSeized;
        public float Gearbox, Engine, EngineMguh, EngineEs, EngineCe, EngineIce, EngineMguk, EngineTc;
    }

    private readonly record struct GapSample(byte Lap, float AheadMs, float? BehindMs);

    private sealed class Mutable
    {
        public ulong SessionUid;
        public int PacketFormat;
        public bool WrongFormat;
        public int PlayerIndex;
        public DateTimeOffset? LastPacketUtc;
        public long Received;
        public long Dropped;
        public long Malformed;
        public SessionType SessionType;
        public string Track = "";
        public int TrackId;
        public float TrackLengthM;
        public int TotalLaps;
        public float TimeLeftS;
        public Weather Weather;
        public float TrackTempC;
        public float AirTempC;
        public SafetyCarStatus SafetyCar;
        public bool Paused;
    }
}
