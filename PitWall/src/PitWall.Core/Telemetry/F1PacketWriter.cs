using PitWall.Contracts;
using PitWall.Core.IO;

namespace PitWall.Core.Telemetry;

/// <summary>
/// Encodes F1 25 UDP packets using the official struct layout. Used by the Sim and decoder tests.
/// Offsets come from the EA F1 25 Telemetry Output Structures document, not from guessed padding.
/// </summary>
public static class F1PacketWriter
{
    public readonly record struct HeaderParams(
        byte PacketId,
        ulong SessionUid,
        float SessionTime,
        uint FrameIdentifier,
        byte PlayerCarIndex = 0,
        byte GameMajorVersion = 1,
        byte GameMinorVersion = 0,
        byte PacketVersion = 1,
        byte SecondaryPlayerCarIndex = 255);

    public static void WriteHeader(LittleEndianBuffer w, in HeaderParams header)
    {
        w.U16(Constants.PacketFormat2025);
        w.U8(25);
        w.U8(header.GameMajorVersion);
        w.U8(header.GameMinorVersion);
        w.U8(header.PacketVersion);
        w.U8(header.PacketId);
        w.U64(header.SessionUid);
        w.F32(header.SessionTime);
        w.U32(header.FrameIdentifier);
        w.U32(header.FrameIdentifier);
        w.U8(header.PlayerCarIndex);
        w.U8(header.SecondaryPlayerCarIndex);
    }

    public static byte[] Session(
        in HeaderParams header,
        byte weather = 0,
        sbyte trackTempC = 32,
        sbyte airTempC = 24,
        byte totalLaps = 5,
        ushort trackLengthM = 5793,
        byte sessionType = 15,
        sbyte trackId = 7,
        ushort sessionTimeLeft = 3600,
        byte safetyCar = 0,
        byte paused = 0,
        byte pitWindowIdeal = 2,
        byte pitWindowLatest = 4)
    {
        var w = new LittleEndianBuffer(PacketSizes.Session);
        WriteHeader(w, header with { PacketId = 1 });
        w.U8(weather);
        w.I8(trackTempC);
        w.I8(airTempC);
        w.U8(totalLaps);
        w.U16(trackLengthM);
        w.U8(sessionType);
        w.I8(trackId);
        w.U8(0); // formula
        w.U16(sessionTimeLeft);
        w.U16(3600); // duration
        w.U8(80); // pit speed limit
        w.U8(paused);
        w.U8(0); // spectating
        w.U8(0); // spectator car
        w.U8(0); // sli
        w.U8(0); // num marshal zones
        w.Pad(PacketSizes.MarshalZones * 5);
        w.U8(safetyCar);
        w.U8(0); // network
        w.U8(1); // one forecast sample
        w.U8(sessionType);
        w.U8(10); // offset min
        w.U8(weather);
        w.I8(trackTempC);
        w.I8(2); // no change
        w.I8(airTempC);
        w.I8(2);
        w.U8(5); // rain %
        w.Pad((PacketSizes.WeatherForecastSamples - 1) * 8);
        w.U8(0); // forecast accuracy
        w.U8(90); // AI difficulty
        w.U32(1);
        w.U32(1);
        w.U32(1);
        w.U8(pitWindowIdeal);
        w.U8(pitWindowLatest);
        w.U8(10); // rejoin
        w.U8(0); // steering assist
        w.U8(0); // braking assist
        w.U8(1); // gearbox assist (manual)
        w.Pad(8); // pit/ERS/DRS/line/game/rule
        w.U32(12 * 60); // time of day minutes
        w.U8(3); // short session length
        w.Pad(7); // units + SC/VSC/red counts
        w.Pad(24); // remaining assists/flags
        w.U8(1); // sessions in weekend
        w.Pad(PacketSizes.SessionsInWeekend);
        w.F32(2000);
        w.F32(4000);
        return w.ToArray();
    }

    public static byte[] LapData(in HeaderParams header, IReadOnlyList<LapCar> cars)
    {
        var w = new LittleEndianBuffer(PacketSizes.Lap);
        WriteHeader(w, header with { PacketId = 2 });
        for (var i = 0; i < PacketSizes.Cars; i++)
        {
            var car = i < cars.Count ? cars[i] : default;
            w.U32(car.LastLapMs);
            w.U32(car.CurrentLapMs);
            WriteMinuteMs(w, car.Sector1Ms);
            WriteMinuteMs(w, car.Sector2Ms);
            WriteMinuteMs(w, car.DeltaToFrontMs);
            WriteMinuteMs(w, car.DeltaToLeaderMs);
            w.F32(car.LapDistance);
            w.F32(car.TotalDistance);
            w.F32(0);
            w.U8(car.Position);
            w.U8(car.Lap);
            w.U8(car.PitStatus);
            w.U8(car.Stops);
            w.U8(car.Sector);
            w.U8(car.Invalid ? (byte)1 : (byte)0);
            w.U8(car.PenaltiesS);
            w.U8(0); // warnings
            w.U8(0);
            w.U8(0);
            w.U8(0);
            w.U8(car.GridPosition);
            w.U8(4); // on track
            w.U8(2); // active
            w.U8(0);
            w.U16(0);
            w.U16(0);
            w.U8(0);
            w.F32(0);
            w.U8(255);
        }

        w.U8(255);
        w.U8(255);
        return w.ToArray();
    }

    public static byte[] CarTelemetry(in HeaderParams header, IReadOnlyList<TelemetryCar> cars)
    {
        var w = new LittleEndianBuffer(PacketSizes.CarTelemetry);
        WriteHeader(w, header with { PacketId = 6 });
        for (var i = 0; i < PacketSizes.Cars; i++)
        {
            var car = i < cars.Count ? cars[i] : default;
            w.U16(car.SpeedKph);
            w.F32(car.Throttle);
            w.F32(car.Steer);
            w.F32(car.Brake);
            w.U8(0);
            w.I8(car.Gear);
            w.U16(car.Rpm);
            w.U8(car.DrsOn ? (byte)1 : (byte)0);
            w.U8(50);
            w.U16(0);
            WriteU16x4(w, car.BrakeC);
            WriteU8x4(w, car.SurfaceC);
            WriteU8x4(w, car.InnerC);
            w.U16(car.EngineTempC);
            WriteF32x4(w, car.PressurePsi);
            w.Pad(4); // surface type
        }

        w.U8(255);
        w.U8(255);
        w.I8(0);
        return w.ToArray();
    }

    public static byte[] CarStatus(in HeaderParams header, IReadOnlyList<StatusCar> cars)
    {
        var w = new LittleEndianBuffer(PacketSizes.CarStatus);
        WriteHeader(w, header with { PacketId = 7 });
        for (var i = 0; i < PacketSizes.Cars; i++)
        {
            var car = i < cars.Count ? cars[i] : default;
            w.U8(0);
            w.U8(1);
            w.U8(car.FuelMix);
            w.U8(55);
            w.U8(0);
            w.F32(car.FuelKg);
            w.F32(110);
            w.F32(car.FuelLaps);
            w.U16(car.MaxRpm);
            w.U16(4000);
            w.U8(8);
            w.U8(car.DrsAllowed ? (byte)1 : (byte)0);
            w.U16(0);
            w.U8(car.ActualCompound);
            w.U8(car.VisualCompound);
            w.U8(car.TyreAgeLaps);
            w.I8(0);
            w.F32(0);
            w.F32(0);
            w.F32(car.ErsStoreJ);
            w.U8(car.ErsMode);
            w.F32(car.ErsHarvestedJ);
            w.F32(0);
            w.F32(car.ErsDeployedJ);
            w.U8(0);
        }

        return w.ToArray();
    }

    public static byte[] CarDamage(in HeaderParams header, IReadOnlyList<DamageCar> cars)
    {
        var w = new LittleEndianBuffer(PacketSizes.CarDamage);
        WriteHeader(w, header with { PacketId = 10 });
        for (var i = 0; i < PacketSizes.Cars; i++)
        {
            var car = i < cars.Count ? cars[i] : default;
            WriteF32x4(w, car.WearPct);
            w.Pad(12); // tyre damage, brakes, blisters
            w.U8(car.FrontWingL);
            w.U8(car.FrontWingR);
            w.U8(car.RearWing);
            w.U8(car.Floor);
            w.U8(car.Diffuser);
            w.U8(car.Sidepod);
            w.U8(car.DrsFault ? (byte)1 : (byte)0);
            w.U8(car.ErsFault ? (byte)1 : (byte)0);
            w.U8(car.Gearbox);
            w.U8(car.Engine);
            w.Pad(6); // engine wear
            w.U8(car.EngineBlown ? (byte)1 : (byte)0);
            w.U8(car.EngineSeized ? (byte)1 : (byte)0);
        }

        return w.ToArray();
    }

    public static byte[] Event(in HeaderParams header, string code, ReadOnlySpan<byte> details = default)
    {
        if (code.Length != PacketSizes.EventCodeLength)
        {
            throw new ArgumentException("Event codes are 4 ASCII characters.", nameof(code));
        }

        var w = new LittleEndianBuffer(PacketSizes.Event);
        WriteHeader(w, header with { PacketId = 3 });
        w.FixedAscii(code, PacketSizes.EventCodeLength);
        if (!details.IsEmpty)
        {
            w.Bytes(details);
            if (details.Length < PacketSizes.EventDetailsLength)
            {
                w.Pad(PacketSizes.EventDetailsLength - details.Length);
            }
        }
        else
        {
            w.Pad(PacketSizes.EventDetailsLength);
        }

        return w.ToArray();
    }

    public static byte[] Participants(in HeaderParams header, IReadOnlyList<ParticipantCar> cars)
    {
        var w = new LittleEndianBuffer(PacketSizes.Participants);
        WriteHeader(w, header with { PacketId = 4 });
        w.U8((byte)Math.Min(cars.Count, PacketSizes.Cars));
        for (var i = 0; i < PacketSizes.Cars; i++)
        {
            var car = i < cars.Count ? cars[i] : default;
            w.U8(car.IsAi ? (byte)1 : (byte)0);
            w.U8(255);
            w.U8(0);
            w.U8(car.TeamId);
            w.U8(0);
            w.U8(car.RaceNumber);
            w.U8(0);
            w.FixedAscii(car.Name ?? "", PacketSizes.ParticipantNameLength);
            w.U8(1);
            w.U8(1);
            w.U16(0);
            w.U8(3); // PlayStation
            w.U8(0);
            w.Pad(12);
        }

        return w.ToArray();
    }

    public static byte[] Motion(in HeaderParams header, IReadOnlyList<MotionCar> cars)
    {
        var w = new LittleEndianBuffer(PacketSizes.Motion);
        WriteHeader(w, header with { PacketId = 0 });
        for (var i = 0; i < PacketSizes.Cars; i++)
        {
            var car = i < cars.Count ? cars[i] : default;
            w.F32(car.WorldX);
            w.F32(0);
            w.F32(car.WorldZ);
            w.Pad(12); // velocity
            w.Pad(12); // dirs
            w.Pad(12); // g
            w.Pad(12); // yaw/pitch/roll
        }

        return w.ToArray();
    }

    private static void WriteMinuteMs(LittleEndianBuffer w, uint totalMs)
    {
        var minutes = (byte)Math.Min(totalMs / 60_000, 255);
        var ms = (ushort)(totalMs % 60_000);
        w.U16(ms);
        w.U8(minutes);
    }

    private static void WriteU16x4(LittleEndianBuffer w, ushort[]? values)
    {
        for (var i = 0; i < 4; i++)
        {
            w.U16(values is { Length: >= 4 } ? values[i] : (ushort)0);
        }
    }

    private static void WriteU8x4(LittleEndianBuffer w, byte[]? values)
    {
        for (var i = 0; i < 4; i++)
        {
            w.U8(values is { Length: >= 4 } ? values[i] : (byte)0);
        }
    }

    private static void WriteF32x4(LittleEndianBuffer w, float[]? values)
    {
        for (var i = 0; i < 4; i++)
        {
            w.F32(values is { Length: >= 4 } ? values[i] : 0f);
        }
    }
}

public readonly record struct LapCar(
    uint LastLapMs,
    uint CurrentLapMs,
    uint Sector1Ms,
    uint Sector2Ms,
    uint DeltaToFrontMs,
    uint DeltaToLeaderMs,
    float LapDistance,
    float TotalDistance,
    byte Position,
    byte Lap,
    byte PitStatus = 0,
    byte Stops = 0,
    byte Sector = 0,
    bool Invalid = false,
    byte PenaltiesS = 0,
    byte GridPosition = 1);

public readonly record struct TelemetryCar(
    ushort SpeedKph,
    float Throttle,
    float Brake,
    float Steer,
    sbyte Gear,
    ushort Rpm,
    bool DrsOn,
    ushort[] BrakeC,
    byte[] SurfaceC,
    byte[] InnerC,
    ushort EngineTempC,
    float[] PressurePsi);

public readonly record struct StatusCar(
    byte FuelMix,
    float FuelKg,
    float FuelLaps,
    ushort MaxRpm,
    bool DrsAllowed,
    byte ActualCompound,
    byte VisualCompound,
    byte TyreAgeLaps,
    float ErsStoreJ,
    byte ErsMode,
    float ErsHarvestedJ,
    float ErsDeployedJ);

public readonly record struct DamageCar(
    float[] WearPct,
    byte FrontWingL = 0,
    byte FrontWingR = 0,
    byte RearWing = 0,
    byte Floor = 0,
    byte Diffuser = 0,
    byte Sidepod = 0,
    bool DrsFault = false,
    bool ErsFault = false,
    byte Gearbox = 0,
    byte Engine = 0,
    bool EngineBlown = false,
    bool EngineSeized = false);

public readonly record struct ParticipantCar(
    string Name,
    byte TeamId,
    byte RaceNumber,
    bool IsAi);

public readonly record struct MotionCar(float WorldX, float WorldZ);
