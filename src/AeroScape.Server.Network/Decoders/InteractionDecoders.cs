using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

/// <summary>
/// Decodes NPC attack packet (opcode 123).
/// Legacy Java NPCAttack: readUnsignedWord (npcId)
/// </summary>
public sealed class NpcAttackDecoder : IPacketDecoder<NpcAttackMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["NpcAttack"];

    public NpcAttackMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int npcIndex = reader.ReadUShort();
        return new NpcAttackMessage(npcIndex);
    }
}

/// <summary>
/// Decodes NPC interact options 1-3.
/// NpcInteract1 (opcode 7): readUnsignedWordA (npcId)
/// NpcInteract2 (opcode 52): readUnsignedWordBigEndianA (npcId)
/// NpcInteract3 (opcode 199): readUnsignedWordBigEndian (npcId)
/// </summary>
public sealed class NpcInteractDecoder : IPacketDecoder<NpcInteractMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["NpcInteract1", "NpcInteract2", "NpcInteract3"];

    public NpcInteractMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int index = reader.ReadUShort(); // All variants read a 2-byte NPC index
        int option = packetName switch
        {
            "NpcInteract1" => 1,
            "NpcInteract2" => 2,
            "NpcInteract3" => 3,
            _ => 1
        };
        return new NpcInteractMessage(index, option);
    }
}

/// <summary>
/// Decodes object interact options 1-2.
/// ObjectInteract1 (opcode 158): readUnsignedWordBigEndian (x), readUnsignedWord (objectId), readUnsignedWordBigEndianA (y)
/// ObjectInteract2 (opcode 228): readUnsignedWord (playerId) — actually an object packet reading differently
/// </summary>
public sealed class ObjectInteractDecoder : IPacketDecoder<ObjectInteractMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ObjectInteract1", "ObjectInteract2", "ObjectBuild"];

    public ObjectInteractMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int option;

        if (packetName == "ObjectInteract1")
        {
            // Legacy Java ObjectOption1: readUnsignedWordBigEndian (x), readUnsignedWord (objectId), readUnsignedWordBigEndianA (y)
            int x = reader.ReadUShort();
            int objectId = reader.ReadUShort();
            int y = reader.ReadUShort();
            option = 1;
            return new ObjectInteractMessage(objectId, x, y, option);
        }
        else if (packetName == "ObjectBuild")
        {
            // Legacy Java (opcode 190): readUnsignedWordBigEndian (y), readUnsignedWordBigEndianA (x), readUnsignedWordBigEndianA (objectId)
            int y = reader.ReadUShort();
            int x = reader.ReadUShort();
            int objectId = reader.ReadUShort();
            option = 3; // build option
            return new ObjectInteractMessage(objectId, x, y, option);
        }
        else
        {
            // ObjectInteract2 (opcode 228): readUnsignedWord (objectId)
            int objectId = reader.ReadUShort();
            option = 2;
            return new ObjectInteractMessage(objectId, 0, 0, option);
        }
    }
}

/// <summary>
/// Decodes player interaction options 1-3.
/// PlayerInteract1 (opcode 160): readUnsignedWordBigEndian (playerId)
/// PlayerInteract2 (opcode 37): readUnsignedWord (playerId)
/// PlayerInteract3 (opcode 227): readUnsignedWordBigEndianA (playerId)
/// </summary>
public sealed class PlayerInteractDecoder : IPacketDecoder<PlayerInteractMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["PlayerInteract1", "PlayerInteract2", "PlayerInteract3"];

    public PlayerInteractMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int index = reader.ReadUShort();
        int option = packetName switch
        {
            "PlayerInteract1" => 1,
            "PlayerInteract2" => 2,
            "PlayerInteract3" => 3,
            _ => 1
        };
        return new PlayerInteractMessage(index, option);
    }
}

/// <summary>
/// Decodes ground item pickup (opcode 201).
/// Legacy Java PickupItem: readUnsignedWordA (y), readUnsignedWord (x), readUnsignedWordBigEndianA (itemId)
/// </summary>
public sealed class GroundItemInteractDecoder : IPacketDecoder<GroundItemInteractMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["GroundItemInteract"];

    public GroundItemInteractMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int y = reader.ReadUShort();
        int x = reader.ReadUShort();
        int itemId = reader.ReadUShort();
        return new GroundItemInteractMessage(itemId, x, y);
    }
}
