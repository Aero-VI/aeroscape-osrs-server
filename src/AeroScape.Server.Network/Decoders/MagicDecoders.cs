using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

/// <summary>
/// Decodes Magic on NPC (opcode 24).
/// Legacy Java MagicOnNPC: readSignedWordA (npcId), readSignedWordA (buttonId), readUnsignedWord (interfaceId)
/// </summary>
public sealed class MagicOnNpcDecoder : IPacketDecoder<MagicOnNpcMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["MagicOnNpc"];

    public MagicOnNpcMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int npcIndex = reader.ReadShortA();
        int spellId = reader.ReadShortA();
        int interfaceId = reader.ReadUShort();
        return new MagicOnNpcMessage(npcIndex, spellId, interfaceId);
    }
}

/// <summary>
/// Decodes Magic on Player (opcode 70).
/// Legacy Java MagicOnPlayer: readSignedWordA (attackPlayer), readSignedWordBigEndian (playerId),
///   readUnsignedWord (interfaceId), readUnsignedWord (clickId/spellId)
/// </summary>
public sealed class MagicOnPlayerDecoder : IPacketDecoder<MagicOnPlayerMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["MagicOnPlayer"];

    public MagicOnPlayerMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        reader.ReadShortA(); // attack player index (junk — handled by engine)
        int targetIndex = reader.ReadShort();
        int interfaceId = reader.ReadUShort();
        int spellId = reader.ReadUShort();
        return new MagicOnPlayerMessage(targetIndex, spellId, interfaceId);
    }
}

/// <summary>
/// Decodes Magic on Item (opcode 154) — e.g. high alchemy on an inventory item.
/// </summary>
public sealed class MagicOnItemDecoder : IPacketDecoder<MagicOnItemMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["MagicOnItem"];

    public MagicOnItemMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int itemId = reader.ReadUShort();
        int slot = reader.ReadUShort();
        int spellId = reader.ReadUShort();
        int interfaceId = reader.ReadUShort();
        return new MagicOnItemMessage(itemId, slot, spellId, interfaceId);
    }
}
