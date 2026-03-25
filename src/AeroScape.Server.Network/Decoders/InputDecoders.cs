using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

/// <summary>
/// Decodes number input (opcode 43).
/// Legacy Java: readDWord (int value)
/// </summary>
public sealed class NumberInputDecoder : IPacketDecoder<NumberInputMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["NumberInput"];

    public NumberInputMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int value = reader.ReadInt();
        return new NumberInputMessage(value);
    }
}

/// <summary>
/// Decodes string input (opcode 127).
/// Legacy Java: readString (string value)
/// </summary>
public sealed class StringInputDecoder : IPacketDecoder<StringInputMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["StringInput"];

    public StringInputMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        string value = reader.ReadString();
        return new StringInputMessage(value);
    }
}

/// <summary>
/// Decodes long input (opcode 189).
/// Legacy Java: readQWord (long value)
/// </summary>
public sealed class LongInputDecoder : IPacketDecoder<LongInputMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["LongInput"];

    public LongInputMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        long value = reader.ReadLong();
        return new LongInputMessage(value, 0);
    }
}
