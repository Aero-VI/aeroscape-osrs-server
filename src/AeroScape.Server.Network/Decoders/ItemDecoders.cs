using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

public sealed class EquipItemDecoder : IPacketDecoder<EquipItemMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["EquipItem"];

    public EquipItemMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int itemId = reader.ReadShort();
        int slot = reader.ReadShortA();
        int interfaceId = reader.ReadShort();
        return new EquipItemMessage(itemId, slot, interfaceId);
    }
}

public sealed class UnequipItemDecoder : IPacketDecoder<UnequipItemMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["UnequipItem"];

    public UnequipItemMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int interfaceId = reader.ReadShort();
        int slot = reader.ReadShort();
        return new UnequipItemMessage(slot, interfaceId);
    }
}

public sealed class DropItemDecoder : IPacketDecoder<DropItemMessage>
{
    // Legacy Java (opcode 211): readDWord (junk), readUnsignedWordBigEndianA (slot), readUnsignedWord (itemId)
    public IReadOnlyList<string> PacketNames { get; } = ["DropItem"];

    public DropItemMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        reader.ReadInt(); // junk
        int slot = reader.ReadUShort();  // readUnsignedWordBigEndianA → big-endian unsigned short
        int itemId = reader.ReadUShort();
        return new DropItemMessage(itemId, slot, 149); // inventory interface
    }
}

public sealed class MoveItemDecoder : IPacketDecoder<MoveItemMessage>
{
    // Legacy Java SwitchItems (opcode 167): toId=readUnsignedWordBigEndianA, junk byte, fromId=readUnsignedWordBigEndianA, junk word, interfaceId=readUnsignedByte, junk byte
    public IReadOnlyList<string> PacketNames { get; } = ["MoveItem"];

    public MoveItemMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int toSlot = reader.ReadUShort();
        reader.ReadByte(); // junk
        int fromSlot = reader.ReadUShort();
        reader.ReadUShort(); // junk
        int interfaceId = reader.ReadByte();
        reader.ReadByte(); // junk
        return new MoveItemMessage(interfaceId, fromSlot, toSlot);
    }
}

/// <summary>
/// Decodes the extended switch-item packet (opcode 179) used for bank operations.
/// Legacy Java SwitchItems2: toInterface=readDWord, fromInterface=readDWord, fromId=readUnsignedWord, toId=readUnsignedWordBigEndian
/// </summary>
public sealed class MoveItemExtendedDecoder : IPacketDecoder<SwitchItemExtendedMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["MoveItemExtended"];

    public SwitchItemExtendedMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int toInterfaceHash = reader.ReadInt();
        int fromInterfaceHash = reader.ReadInt();
        int fromSlot = reader.ReadUShort();
        int toSlot = reader.ReadUShort();
        return new SwitchItemExtendedMessage(fromSlot, toSlot, fromInterfaceHash, toInterfaceHash);
    }
}

/// <summary>
/// Decodes item operate packet (opcode 186) — operating an equipped item (e.g. glory teleport).
/// Legacy Java ItemOperate: readDWord (junk), readUnsignedWordA (itemId), readUnsignedWordBigEndianA (slot)
/// </summary>
public sealed class ItemOperateDecoder : IPacketDecoder<ItemOperateMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ItemOperate"];

    public ItemOperateMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int interfaceHash = reader.ReadInt();
        int itemId = reader.ReadShortA();
        int slot = reader.ReadUShort();
        return new ItemOperateMessage(itemId, slot, interfaceHash);
    }
}

/// <summary>
/// Decodes ItemOption1 (opcodes 203, 152) — first inventory click option.
/// Legacy Java: readUnsignedWordBigEndianA (slot), readUnsignedWord (interfaceId), readUnsignedWord (junk), readUnsignedWord (itemId)
/// </summary>
public sealed class ItemOption1Decoder : IPacketDecoder<ItemOption1Message>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ItemOption1", "ItemOption1Alt"];

    public ItemOption1Message Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int slot = reader.ReadUShort();
        int interfaceId = reader.ReadUShort();
        reader.ReadUShort(); // junk
        int itemId = reader.ReadUShort();
        return new ItemOption1Message(itemId, slot, interfaceId);
    }
}

/// <summary>
/// Decodes ItemOption2 — same format as ItemOption1.
/// </summary>
public sealed class ItemOption2Decoder : IPacketDecoder<ItemOption2Message>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ItemOption2"];

    public ItemOption2Message Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int slot = reader.ReadUShort();
        int interfaceId = reader.ReadUShort();
        reader.ReadUShort(); // junk
        int itemId = reader.ReadUShort();
        return new ItemOption2Message(itemId, slot, interfaceId);
    }
}

/// <summary>
/// Decodes ItemSelect (opcodes 220, 134) — eating, drinking, etc.
/// Uses same format as ItemOption1.
/// </summary>
public sealed class ItemSelectDecoder : IPacketDecoder<ItemSelectMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ItemSelect", "ItemSelectAlt"];

    public ItemSelectMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int slot = reader.ReadUShort();
        int interfaceId = reader.ReadUShort();
        reader.ReadUShort(); // junk
        int itemId = reader.ReadUShort();
        return new ItemSelectMessage(itemId, slot, interfaceId);
    }
}

/// <summary>
/// Decodes ItemOnItem (opcode 40).
/// Legacy Java: readSignedWordBigEndian (usedWith), readSignedWordA (itemUsed)
/// </summary>
public sealed class ItemOnItemDecoder : IPacketDecoder<ItemOnItemMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ItemOnItem"];

    public ItemOnItemMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int usedWith = reader.ReadShort();
        int itemUsed = reader.ReadShortA();
        return new ItemOnItemMessage(itemUsed, usedWith);
    }
}

/// <summary>
/// Decodes ItemOnNpc (opcode 131).
/// Legacy Java: readSignedWordA (usedWith/itemId), readSignedWordBigEndian (itemUsed/npcIndex)
/// We interpret as: itemId, npcIndex.
/// </summary>
public sealed class ItemOnNpcDecoder : IPacketDecoder<ItemOnNpcMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ItemOnNpc"];

    public ItemOnNpcMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int itemId = reader.ReadShortA();
        int npcIndex = reader.ReadShort();
        return new ItemOnNpcMessage(itemId, npcIndex, 0, 0);
    }
}

/// <summary>
/// Decodes ItemOnObject (opcode 224).
/// Legacy Java: readUnsignedWord (objectId?), readSignedWordA (itemId)
/// </summary>
public sealed class ItemOnObjectDecoder : IPacketDecoder<ItemOnObjectMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ItemOnObject"];

    public ItemOnObjectMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int objectId = reader.ReadUShort();
        int itemId = reader.ReadShortA();
        return new ItemOnObjectMessage(itemId, objectId, 0, 0);
    }
}

/// <summary>
/// Decodes ItemOnPlayer (opcode 253 — trade accept variant).
/// Legacy Java: readUnsignedWord (playerId) 
/// </summary>
public sealed class ItemOnPlayerDecoder : IPacketDecoder<ItemOnPlayerMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["ItemOnPlayer"];

    public ItemOnPlayerMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int targetIndex = reader.ReadUShort();
        return new ItemOnPlayerMessage(0, targetIndex);
    }
}
