using PitWall.Contracts;
using PitWall.Contracts.Enums;
using PitWall.Core.Recording;
using PitWall.Core.State;
using PitWall.Core.Telemetry;

namespace PitWall.Core.Tests;

public sealed class TelemetryRoundTripTests
{
    [Fact]
    public void WriterPacketsDecodeAndPopulatePlayerSnapshot()
    {
        var decoder = new PacketDecoder();
        var store = new StateStore();
        const ulong sessionUid = 123456;
        uint frame = 1;
        var at = DateTimeOffset.UtcNow;
        var header = new F1PacketWriter.HeaderParams(0, sessionUid, 1.5f, frame++);
        var telemetry = new TelemetryCar(287, .82f, .04f, -.12f, 7, 12100, true,
            [410, 411, 412, 413], [90, 91, 92, 93], [96, 97, 98, 99], 101,
            [22.1f, 22.2f, 22.3f, 22.4f]);
        var packets = new[]
        {
            F1PacketWriter.Session(header, trackId: 2, totalLaps: 5),
            F1PacketWriter.Participants(header, [new ParticipantCar("Player", 0, 1, false)]),
            F1PacketWriter.LapData(header, [new LapCar(91_234, 32_100, 29_000, 60_000, 850, 1_700, 1_200, 7_000, 1, 2)]),
            F1PacketWriter.CarTelemetry(header, [telemetry]),
            F1PacketWriter.CarStatus(header, [new StatusCar(1, 36.5f, 1.2f, 15000, true, 16, 16, 2, 3_500_000, 2, 1_000_000, 900_000)]),
            F1PacketWriter.CarDamage(header, [new DamageCar([3f, 4f, 5f, 6f])]),
            F1PacketWriter.Event(header, "SSTA"),
            F1PacketWriter.Motion(header, [new MotionCar(10, 20)]),
            F1PacketWriter.SessionHistory(header, new SessionHistoryData(0,
                [new SessionLapHistory(91_000, 29_000, 30_000, 32_000)],
                [new TyreStintHistory(255, 16, 16)])),
            F1PacketWriter.TyreSets(header, new TyreSetsData(0,
                [new TyreSet(16, 16, 3, true, 15, 12, 20, -125, true)]))
        };

        foreach (var bytes in packets)
        {
            Assert.Contains(bytes.Length, new[] { PacketSizes.Motion, PacketSizes.Session, PacketSizes.Lap,
                PacketSizes.Event, PacketSizes.Participants, PacketSizes.CarTelemetry, PacketSizes.CarStatus,
                PacketSizes.CarDamage, PacketSizes.SessionHistory, PacketSizes.TyreSets });
            Assert.True(decoder.TryDecode(bytes, out var packet, out var status));
            Assert.Equal(DecodeStatus.Ok, status);
            Assert.Equal(Constants.PacketFormat2025, packet.Header.PacketFormat);
            store.Apply(packet, at);
        }

        var snapshot = store.Current;
        Assert.Equal(sessionUid, snapshot.SessionUid);
        Assert.Equal("Shanghai", snapshot.Track);
        Assert.Equal(5, snapshot.TotalLaps);
        Assert.Equal(1, snapshot.Player.Position);
        Assert.Equal(2, snapshot.Player.Lap);
        Assert.Equal(91_234, snapshot.Player.LastLapMs);
        Assert.Equal(287, snapshot.Driver.SpeedKph);
        Assert.Equal(7, snapshot.Driver.Gear);
        Assert.Equal(.82f, snapshot.Driver.Throttle, 2);
        Assert.Equal(DrsState.Active, snapshot.Driver.Drs);
        Assert.Equal(36.5f, snapshot.Energy.FuelKg);
        Assert.Equal(TyreCompound.Soft, snapshot.VisualCompound);
        Assert.Equal(2, snapshot.TyreAgeLaps);
        Assert.Equal(3f, snapshot.Tyres.WearPct[0]);
        Assert.Equal(Constants.MaxCars, snapshot.FieldCars.Count);
        Assert.Equal("Player", snapshot.FieldCars[0].Name);
        Assert.True(snapshot.FieldCars[0].IsPlayer);
        var field = SnapshotBuilder.BuildField(snapshot);
        Assert.Equal(Constants.MaxCars, field.Cars.Length);
        Assert.Equal(5, field.Session.TotalLaps);
        var live = SnapshotBuilder.BuildLive(snapshot);
        Assert.Equal(287, live.Driver.SpeedKph);
        Assert.Single(snapshot.Events);
        Assert.Equal(0, decoder.Malformed);
    }

    [Fact]
    public void RecorderRoundTripsTimestampedDatagrams()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pitwall-test-{Guid.NewGuid():N}");
        var started = new DateTimeOffset(2026, 10, 30, 18, 0, 0, TimeSpan.Zero);
        var first = F1PacketWriter.Event(new F1PacketWriter.HeaderParams(3, 45, 1, 1), "SSTA");
        var second = F1PacketWriter.Event(new F1PacketWriter.HeaderParams(3, 45, 2, 2), "SEND");

        try
        {
            using var recorder = new Recorder();
            var path = recorder.Start(directory, "Shanghai", SessionType.Race, started);
            recorder.Write(first, started.AddMilliseconds(100));
            recorder.Write(second, started.AddMilliseconds(275));
            recorder.Stop();

            using var stream = File.OpenRead(path);
            var (recordingStart, records) = PwrecFile.Read(stream);
            Assert.Equal(started, recordingStart);
            Assert.Equal(2, records.Count);
            Assert.Equal(100u, records[0].Milliseconds);
            Assert.Equal(275u, records[1].Milliseconds);
            Assert.Equal(first, records[0].Datagram);
            Assert.Equal(second, records[1].Datagram);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DecoderRejectsTruncatedAndOutOfOrderPackets()
    {
        var decoder = new PacketDecoder();
        var header = new F1PacketWriter.HeaderParams(0, 55, 1, 8);
        var packet = F1PacketWriter.Motion(header, []);

        Assert.False(decoder.TryDecode(packet.AsSpan(0, 10), out _, out var truncatedStatus));
        Assert.Equal(DecodeStatus.Failed, truncatedStatus);
        Assert.True(decoder.TryDecode(packet, out _, out _));
        var older = F1PacketWriter.Motion(header with { FrameIdentifier = 7 }, []);
        Assert.False(decoder.TryDecode(older, out _, out var staleStatus));
        Assert.Equal(DecodeStatus.StaleFrame, staleStatus);
        Assert.Equal(1, decoder.Malformed);
        Assert.Equal(1, decoder.DroppedOld);
    }

}
