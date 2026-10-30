using System.Globalization;
using System.Net;
using System.Net.Sockets;
using PitWall.Core.Recording;
using PitWall.Core.Telemetry;

var address = IPAddress.Loopback;
var port = 20777;
var hz = 10;
string? replayPath = null;
var replaySpeed = 1d;
var loopReplay = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--address" when i + 1 < args.Length:
            if (!IPAddress.TryParse(args[++i], out var parsedAddress))
                return Usage("--address must be an IPv4 or IPv6 address.");
            address = parsedAddress;
            break;
        case "--port" when i + 1 < args.Length:
            if (!int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535)
                return Usage("--port must be between 1 and 65535.");
            break;
        case "--hz" when i + 1 < args.Length:
            if (!int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out hz) || hz is < 1 or > 60)
                return Usage("--hz must be between 1 and 60.");
            break;
        case "--replay" when i + 1 < args.Length:
            replayPath = args[++i];
            break;
        case "--speed" when i + 1 < args.Length:
            if (!double.TryParse(args[++i], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out replaySpeed) || replaySpeed is not (1 or 2 or 4))
                return Usage("--speed must be 1, 2 or 4.");
            break;
        case "--loop":
            loopReplay = true;
            break;
        case "--help":
        case "-h":
            return Usage(null);
        default:
            return Usage($"Unknown or incomplete option: {args[i]}");
    }
}

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stop.Cancel();
};

if (replayPath is not null)
{
    Console.WriteLine($"Replaying {replayPath} → {address}:{port} at {replaySpeed:0.##}x" + (loopReplay ? " (looping)" : ""));
    await Replayer.ReplayToUdpAsync(replayPath, port, replaySpeed, loopReplay, stop.Token, address);
    Console.WriteLine("Replay finished");
    return 0;
}

using var udp = new UdpClient(address.AddressFamily);
var endpoint = new IPEndPoint(address, port);
var sessionUid = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var tick = 0;
var cars = Enumerable.Range(0, 22).Select(i => new SimCar(i)).ToArray();
var header = Header(0);

await Send(F1PacketWriter.Session(header, trackId: 2, totalLaps: 5));
await Send(F1PacketWriter.Participants(header, cars.Select(c => new ParticipantCar(c.Name, (byte)(c.Index % 11), (byte)(1 + c.Index), c.Index != 0)).ToArray()));
await Send(F1PacketWriter.Event(header, "SSTA"));

Console.WriteLine($"PitWall synthetic race → {endpoint} at {hz} Hz (Ctrl+C to stop)");
var interval = TimeSpan.FromSeconds(1d / hz);
try
{
    while (!stop.IsCancellationRequested)
    {
        var started = DateTime.UtcNow;
        var elapsedSeconds = tick / (double)hz;
        foreach (var car in cars)
            car.Advance(elapsedSeconds, hz);

        var h = Header((uint)tick);
        await Send(F1PacketWriter.LapData(h, cars.Select(c => c.Lap).ToArray()));
        await Send(F1PacketWriter.CarTelemetry(h, cars.Select(c => c.Telemetry).ToArray()));
        await Send(F1PacketWriter.CarStatus(h, cars.Select(c => c.Status).ToArray()));
        await Send(F1PacketWriter.CarDamage(h, cars.Select(c => c.Damage).ToArray()));
        await Send(F1PacketWriter.Motion(h, cars.Select(c => new MotionCar(c.Distance, (float)Math.Sin(c.Distance / 500))).ToArray()));
        var historyCar = cars[tick % cars.Length];
        await Send(F1PacketWriter.SessionHistory(h, historyCar.History));
        await Send(F1PacketWriter.TyreSets(h, historyCar.TyreSets));
        tick++;

        var delay = interval - (DateTime.UtcNow - started);
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, stop.Token);
    }
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
}

return 0;

F1PacketWriter.HeaderParams Header(uint frameId) => new(0, sessionUid, tick / (float)hz, frameId);

async Task Send(byte[] packet) => await udp.SendAsync(packet, endpoint, stop.Token);

static int Usage(string? error)
{
    if (error is not null)
        Console.Error.WriteLine(error);
    Console.WriteLine("Usage:");
    Console.WriteLine("  PitWall.Sim [--address 127.0.0.1] [--port 20777] [--hz 10]");
    Console.WriteLine("  PitWall.Sim --replay <file.pwrec> [--speed 1|2|4] [--loop] [--address 127.0.0.1] [--port 20777]");
    return error is null ? 0 : 2;
}

sealed class SimCar(int index)
{
    public int Index { get; } = index;
    public string Name { get; } = index == 0 ? "Player" : $"Driver {index + 1:00}";
    public float Distance { get; private set; }
    private uint LapNumber => (uint)(1 + (Distance / 5793));
    private float LapDistance => Distance % 5793;
    private float Speed => 255 + 22 * MathF.Sin((LapDistance / 5793) * MathF.Tau + Index * .25f);

    public void Advance(double seconds, int hz) => Distance = (float)((Index * 81 + seconds * (Speed * 1000 / 3600)) % (5793 * 6));

    public LapCar Lap => new(
        LastLapMs: 91_000u + (uint)(Index * 173),
        CurrentLapMs: (uint)(LapDistance / Math.Max(1, Speed) * 3600),
        Sector1Ms: 29_000,
        Sector2Ms: 60_000,
        DeltaToFrontMs: (uint)(Index * 850),
        DeltaToLeaderMs: (uint)(Index * 850),
        LapDistance,
        Distance,
        Position: (byte)(Index + 1),
        Lap: (byte)Math.Min(5, LapNumber),
        Sector: (byte)Math.Min(2, LapDistance / 1931));

    public TelemetryCar Telemetry => new(
        (ushort)Math.Clamp(Speed, 0, 360),
        .65f + .3f * MathF.Sin(LapDistance / 5793 * MathF.Tau),
        0,
        .2f * MathF.Sin(LapDistance / 5793 * MathF.Tau),
        (sbyte)Math.Clamp((int)(Speed / 38), 1, 8),
        (ushort)Math.Clamp(Speed * 38, 0, 15000),
        LapDistance is > 500 and < 1200,
        [420, 415, 410, 408],
        [92, 93, 91, 92],
        [98, 99, 97, 98],
        102,
        [22.5f, 22.6f, 22.3f, 22.4f]);

    public StatusCar Status => new(1, 38 - LapNumber * .7f, 1.4f, 15000, true, 16, 16, (byte)LapNumber, 3_800_000, 2, 1_100_000, 850_000);
    public DamageCar Damage => new([3.1f + Index * .1f, 3.2f + Index * .1f, 2.9f + Index * .1f, 3.0f + Index * .1f]);
    public SessionHistoryData History => new(
        (byte)Index,
        Enumerable.Range(0, (int)Math.Min(5, LapNumber)).Select(lap => new SessionLapHistory(
            91_000u + (uint)(Index * 173), 29_000, 30_000, 32_000)).ToArray(),
        [new TyreStintHistory(255, 16, 16)]);
    public TyreSetsData TyreSets => new((byte)Index,
        [new TyreSet(16, 16, (byte)Math.Clamp(Index, 0, 99), true, 15, 12, 20, 0, true)]);
}
