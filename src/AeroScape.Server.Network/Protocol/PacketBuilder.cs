using System.Buffers.Binary;
using System.Text;
using AeroScape.Server.Core.Crypto;

namespace AeroScape.Server.Network.Protocol;

/// <summary>
/// Mutable packet writer for building outgoing packets.
/// Supports variable-length headers and ISAAC encryption.
/// </summary>
public sealed class PacketBuilder
{
    private byte[] _buffer;
    private int _position;
    private int _bitPosition;
    private bool _bitMode;

    public PacketBuilder(int initialCapacity = 256)
    {
        _buffer = new byte[initialCapacity];
    }

    public int Position => _position;

    private void EnsureCapacity(int additional)
    {
        if (_position + additional > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _position + additional));
    }

    public PacketBuilder WriteByte(int value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = (byte)value;
        return this;
    }

    public PacketBuilder WriteShort(int value)
    {
        EnsureCapacity(2);
        BinaryPrimitives.WriteInt16BigEndian(_buffer.AsSpan(_position), (short)value);
        _position += 2;
        return this;
    }

    public PacketBuilder WriteInt(int value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteInt32BigEndian(_buffer.AsSpan(_position), value);
        _position += 4;
        return this;
    }

    public PacketBuilder WriteLong(long value)
    {
        EnsureCapacity(8);
        BinaryPrimitives.WriteInt64BigEndian(_buffer.AsSpan(_position), value);
        _position += 8;
        return this;
    }

    public PacketBuilder WriteMedium(int value)
    {
        EnsureCapacity(3);
        _buffer[_position++] = (byte)(value >> 16);
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)value;
        return this;
    }

    public PacketBuilder WriteByteA(int value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = (byte)(value + 128);
        return this;
    }

    public PacketBuilder WriteByteC(int value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = (byte)(-value);
        return this;
    }

    public PacketBuilder WriteByteS(int value)
    {
        EnsureCapacity(1);
        _buffer[_position++] = (byte)(128 - value);
        return this;
    }

    public PacketBuilder WriteShortA(int value)
    {
        EnsureCapacity(2);
        _buffer[_position++] = (byte)(value >> 8);
        _buffer[_position++] = (byte)(value + 128);
        return this;
    }

    public PacketBuilder WriteLEShort(int value)
    {
        EnsureCapacity(2);
        _buffer[_position++] = (byte)value;
        _buffer[_position++] = (byte)(value >> 8);
        return this;
    }

    public PacketBuilder WriteLEShortA(int value)
    {
        EnsureCapacity(2);
        _buffer[_position++] = (byte)(value + 128);
        _buffer[_position++] = (byte)(value >> 8);
        return this;
    }

    public PacketBuilder WriteString(string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        EnsureCapacity(bytes.Length + 1);
        bytes.CopyTo(_buffer.AsSpan(_position));
        _position += bytes.Length;
        _buffer[_position++] = 10; // newline delimiter
        return this;
    }

    public PacketBuilder WriteBytes(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(data.Length);
        data.CopyTo(_buffer.AsSpan(_position));
        _position += data.Length;
        return this;
    }

    // Bit access methods for player/NPC updating
    public void InitBitAccess()
    {
        _bitPosition = _position * 8;
        _bitMode = true;
    }

    public void WriteBits(int numBits, int value)
    {
        int bytePos = _bitPosition >> 3;
        int bitOffset = 8 - (_bitPosition & 7);
        _bitPosition += numBits;

        EnsureCapacity((_bitPosition + 7) / 8 - _position + 1);

        for (; numBits > bitOffset; bitOffset = 8)
        {
            _buffer[bytePos] &= (byte)~BitMask(bitOffset);
            _buffer[bytePos++] |= (byte)((value >> (numBits - bitOffset)) & BitMask(bitOffset));
            numBits -= bitOffset;
        }

        if (numBits == bitOffset)
        {
            _buffer[bytePos] &= (byte)~BitMask(bitOffset);
            _buffer[bytePos] |= (byte)(value & BitMask(bitOffset));
        }
        else
        {
            _buffer[bytePos] &= (byte)~(BitMask(numBits) << (bitOffset - numBits));
            _buffer[bytePos] |= (byte)((value & BitMask(numBits)) << (bitOffset - numBits));
        }
    }

    public void FinishBitAccess()
    {
        _position = (_bitPosition + 7) / 8;
        _bitMode = false;
    }

    private static int BitMask(int bits) => (1 << bits) - 1;

    /// <summary>
    /// Builds the final packet with opcode header and optional ISAAC encryption.
    /// </summary>
    public ReadOnlyMemory<byte> Build(int opcode, IsaacRandom? isaac = null)
    {
        // Encrypt opcode
        int encOpcode = isaac != null ? (opcode + isaac.NextInt()) & 0xFF : opcode;
        
        var result = new byte[_position + 1]; // opcode + payload
        result[0] = (byte)encOpcode;
        Buffer.BlockCopy(_buffer, 0, result, 1, _position);
        return result;
    }

    /// <summary>
    /// Builds a variable-byte packet (size as 1 byte after opcode).
    /// </summary>
    public ReadOnlyMemory<byte> BuildVarByte(int opcode, IsaacRandom? isaac = null)
    {
        int encOpcode = isaac != null ? (opcode + isaac.NextInt()) & 0xFF : opcode;
        var result = new byte[_position + 2]; // opcode + size_byte + payload
        result[0] = (byte)encOpcode;
        result[1] = (byte)_position;
        Buffer.BlockCopy(_buffer, 0, result, 2, _position);
        return result;
    }

    /// <summary>
    /// Builds a variable-short packet (size as 2 bytes after opcode).
    /// </summary>
    public ReadOnlyMemory<byte> BuildVarShort(int opcode, IsaacRandom? isaac = null)
    {
        int encOpcode = isaac != null ? (opcode + isaac.NextInt()) & 0xFF : opcode;
        var result = new byte[_position + 3]; // opcode + size_short + payload
        result[0] = (byte)encOpcode;
        BinaryPrimitives.WriteInt16BigEndian(result.AsSpan(1), (short)_position);
        Buffer.BlockCopy(_buffer, 0, result, 3, _position);
        return result;
    }

    /// <summary>
    /// Returns the raw payload bytes without any header.
    /// </summary>
    public ReadOnlyMemory<byte> BuildRaw()
    {
        var result = new byte[_position];
        Buffer.BlockCopy(_buffer, 0, result, 0, _position);
        return result;
    }

    public void Reset() => _position = 0;
}
