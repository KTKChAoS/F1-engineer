using PitWall.Contracts.Enums;
using PitWall.Contracts.Messages;

namespace PitWall.Core.State;

public sealed class RaceSnapshot
{
    public static RaceSnapshot Empty { get; } = new();

    public ConnectionState Connection { get; init; } = ConnectionState.Waiting;
    public int Pps { get; init; }
    public int PacketFormat { get; init; }
    public bool WrongPacketFormat { get; init; }
    public DateTimeOffset? LastPacketUtc { get; init; }
    public TimeSpan LastPacketAge { get; init; } = Timeout.InfiniteTimeSpan;
    public long Received { get; init; }
    public long Dropped { get; init; }
    public long Malformed { get; init; }

    public ulong SessionUid { get; init; }
    public SessionType SessionType { get; init; }
    public string Track { get; init; } = "";
    public int TrackId { get; init; }
    public float TrackLengthM { get; init; }
    public int TotalLaps { get; init; }
    public float TimeLeftS { get; init; }
    public Weather Weather { get; init; }
    public float TrackTempC { get; init; }
    public float AirTempC { get; init; }
    public SafetyCarStatus SafetyCar { get; init; }
    public bool Paused { get; init; }

    public PlayerInfo Player { get; init; } = new();
    public DriverInfo Driver { get; init; } = new();
    public TyreInfo Tyres { get; init; } = new()
    {
        SurfaceC = new float[4],
        InnerC = new float[4],
        PressurePsi = new float[4],
        BrakeC = new float[4],
        WearPct = new float[4]
    };
    public EnergyInfo Energy { get; init; } = new();
    public TyreCompound VisualCompound { get; init; }
    public int TyreAgeLaps { get; init; }
    public IReadOnlyList<EventRecord> Events { get; init; } = [];
}
