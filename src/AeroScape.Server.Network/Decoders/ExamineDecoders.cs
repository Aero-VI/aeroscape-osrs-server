using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

/// <summary>
/// Decodes item examine (opcode 38).
/// Legacy Java: readUnsignedWordBigEndianA (itemId)
/// </summary>
public sealed class ExamineItemDecoder : IPacketDecoder<ExamineItemMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ExamineItem"];

    public ExamineItemMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int itemId = reader.ReadUShort();
        return new ExamineItemMessage(itemId);
    }
}

/// <summary>
/// Decodes NPC examine (opcode 88).
/// Legacy Java: readUnsignedWord (npcId)
/// </summary>
public sealed class ExamineNpcDecoder : IPacketDecoder<ExamineNpcMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ExamineNpc"];

    public ExamineNpcMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int npcId = reader.ReadUShort();
        return new ExamineNpcMessage(npcId);
    }
}

/// <summary>
/// Decodes object examine (opcode 84).
/// Legacy Java: readUnsignedWordA (objectId)
/// </summary>
public sealed class ExamineObjectDecoder : IPacketDecoder<ExamineObjectMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ExamineObject"];

    public ExamineObjectMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int objectId = reader.ReadShortA();
        return new ExamineObjectMessage(objectId);
    }
}
