using System.Buffers.Binary;
using System.Text;
using PitWall.Contracts;

namespace PitWall.Core.Recording;

public readonly record struct PwrecRecord(uint Milliseconds, byte[] Datagram);

/// <summary>
/// .pwrec layout: magic PWRC, version 1, start time UTC (unix ms int64 LE),
/// then records of [uint32 ms since start][uint16 length][bytes]. All little-endian.
/// </summary>
public static class PwrecFile
{
    public static readonly byte[] Magic = Encoding.ASCII.GetBytes(Constants.RecorderMagic);

    public static void WriteHeader(Stream stream, DateTimeOffset startUtc)
    {
        stream.Write(Magic);
        stream.WriteByte(Constants.RecorderVersion);
        Span<byte> time = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(time, startUtc.ToUnixTimeMilliseconds());
        stream.Write(time);
    }

    public static void WriteRecord(Stream stream, uint milliseconds, ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Datagram exceeds 65535 bytes.");
        }

        Span<byte> prefix = stackalloc byte[6];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, milliseconds);
        BinaryPrimitives.WriteUInt16LittleEndian(prefix[4..], (ushort)datagram.Length);
        stream.Write(prefix);
        stream.Write(datagram);
    }

    public static (DateTimeOffset StartUtc, List<PwrecRecord> Records) Read(Stream stream)
    {
        Span<byte> header = stackalloc byte[13];
        if (stream.Read(header) != 13)
        {
            throw new InvalidDataException("Recording header is truncated.");
        }

        if (!header[..4].SequenceEqual(Magic))
        {
            throw new InvalidDataException("File is not a PitWall recording (missing PWRC magic).");
        }

        if (header[4] != Constants.RecorderVersion)
        {
            throw new InvalidDataException($"Unsupported recording version {header[4]}.");
        }

        var start = DateTimeOffset.FromUnixTimeMilliseconds(BinaryPrimitives.ReadInt64LittleEndian(header[5..]));
        var records = new List<PwrecRecord>();
        Span<byte> prefix = stackalloc byte[6];
        while (true)
        {
            var read = stream.Read(prefix);
            if (read == 0)
            {
                break;
            }

            if (read != 6)
            {
                throw new InvalidDataException("Truncated recording record header.");
            }

            var ms = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(prefix[4..]);
            var datagram = new byte[length];
            if (stream.Read(datagram) != length)
            {
                throw new InvalidDataException("Truncated recording datagram.");
            }

            records.Add(new PwrecRecord(ms, datagram));
        }

        return (start, records);
    }
}
