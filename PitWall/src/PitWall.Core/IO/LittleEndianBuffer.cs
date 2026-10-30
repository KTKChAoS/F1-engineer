using System.Buffers.Binary;
using System.Text;

namespace PitWall.Core.IO;

/// <summary>
/// Sequential little-endian writer used to encode F1 25 UDP packets from the official struct layout.
/// </summary>
public sealed class LittleEndianBuffer
{
    private readonly byte[] _buffer;
    private int _pos;

    public LittleEndianBuffer(int size)
    {
        _buffer = new byte[size];
    }

    public int Position => _pos;
    public int Length => _buffer.Length;

    public void U8(byte value)
    {
        _buffer[_pos++] = value;
    }

    public void I8(sbyte value) => U8(unchecked((byte)value));

    public void U16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 2;
    }

    public void I16(short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 2;
    }

    public void U32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 4;
    }

    public void I32(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 4;
    }

    public void U64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 8;
    }

    public void F32(float value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(_pos), value);
        _pos += 4;
    }

    public void Bytes(ReadOnlySpan<byte> data)
    {
        data.CopyTo(_buffer.AsSpan(_pos));
        _pos += data.Length;
    }

    public void Pad(int count)
    {
        _pos += count;
    }

    public void FixedAscii(string value, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        var n = Math.Min(bytes.Length, length);
        bytes.AsSpan(0, n).CopyTo(_buffer.AsSpan(_pos));
        _pos += length;
    }

    public byte[] ToArray()
    {
        if (_pos != _buffer.Length)
        {
            throw new InvalidOperationException($"Buffer expected {_buffer.Length} bytes, wrote {_pos}.");
        }

        return _buffer;
    }
}
