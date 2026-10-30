using System.Net;
using System.Net.Sockets;

namespace PitWall.Core.Recording;

public sealed class Replayer
{
    public static async Task ReplayToUdpAsync(
        string path,
        int port,
        double speed,
        bool loop,
        CancellationToken cancellationToken,
        IPAddress? address = null)
    {
        await using var file = File.OpenRead(path);
        var (_, records) = PwrecFile.Read(file);
        if (records.Count == 0)
        {
            return;
        }

        speed = speed <= 0 ? 1 : speed;
        address ??= IPAddress.Loopback;
        using var udp = new UdpClient();
        var endpoint = new IPEndPoint(address, port);

        do
        {
            var origin = DateTime.UtcNow;
            uint lastMs = 0;
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var delayMs = (record.Milliseconds - lastMs) / speed;
                if (delayMs > 0)
                {
                    var due = origin.AddMilliseconds(record.Milliseconds / speed);
                    var wait = due - DateTime.UtcNow;
                    if (wait > TimeSpan.Zero)
                    {
                        await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                    }
                }

                await udp.SendAsync(record.Datagram, endpoint, cancellationToken).ConfigureAwait(false);
                lastMs = record.Milliseconds;
            }
        } while (loop && !cancellationToken.IsCancellationRequested);
    }

    public static IReadOnlyList<PwrecRecord> ReadRecords(string path)
    {
        using var file = File.OpenRead(path);
        return PwrecFile.Read(file).Records;
    }
}
