using System.Buffers.Binary;
using System.Text;

namespace AeroScape.Server.Network.Protocol;

/// <summary>
/// Zero-copy packet reader over a ReadOnlySpan&lt;byte&gt;.
/// </summary>
public ref struct PacketReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    public PacketReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
    }

    public int Remaining => _data.Length - _position;
    public int Position => _position;

    public byte ReadByte() => _data[_position++];
    
    public sbyte ReadSignedByte() => (sbyte)_data[_position++];

    public short ReadShort()
    {
        var val = BinaryPrimitives.ReadInt16BigEndian(_data[_position..]);
        _position += 2;
        return val;
    }

    public ushort ReadUShort()
    {
        var val = BinaryPrimitives.ReadUInt16BigEndian(_data[_position..]);
        _position += 2;
        return val;
    }

    public int ReadInt()
    {
        var val = BinaryPrimitives.ReadInt32BigEndian(_data[_position..]);
        _position += 4;
        return val;
    }

    public long ReadLong()
    {
        var val = BinaryPrimitives.ReadInt64BigEndian(_data[_position..]);
        _position += 8;
        return val;
    }

    public int ReadMedium()
    {
        int val = (_data[_position++] & 0xFF) << 16;
        val |= (_data[_position++] & 0xFF) << 8;
        val |= _data[_position++] & 0xFF;
        return val;
    }

    // RuneScape-specific byte transformations
    public byte ReadByteA() => (byte)(_data[_position++] - 128);
    public byte ReadByteC() => (byte)(-_data[_position++]);
    public byte ReadByteS() => (byte)(128 - _data[_position++]);

    public short ReadShortA()
    {
        int val = (_data[_position++] & 0xFF) << 8;
        val |= (_data[_position++] - 128) & 0xFF;
        return (short)val;
    }

    public short ReadLEShort()
    {
        int val = _data[_position++] & 0xFF;
        val |= (_data[_position++] & 0xFF) << 8;
        return (short)val;
    }

    public short ReadLEShortA()
    {
        int val = (_data[_position++] - 128) & 0xFF;
        val |= (_data[_position++] & 0xFF) << 8;
        return (short)val;
    }

    public string ReadString()
    {
        int start = _position;
        while (_position < _data.Length && _data[_position] != 10)
            _position++;
        var str = Encoding.ASCII.GetString(_data[start.._position]);
        if (_position < _data.Length) _position++; // skip delimiter
        return str;
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        var span = _data.Slice(_position, count);
        _position += count;
        return span;
    }

    public void Skip(int count) => _position += count;
}
