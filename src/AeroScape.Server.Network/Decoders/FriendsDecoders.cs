using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

/// <summary>
/// Decodes add friend (opcode 30).
/// Legacy Java: readQWord (name as long)
/// </summary>
public sealed class AddFriendDecoder : IPacketDecoder<AddFriendMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["AddFriend"];

    public AddFriendMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        return new AddFriendMessage(reader.ReadLong());
    }
}

/// <summary>
/// Decodes remove friend (opcode 132).
/// Legacy Java: readQWord (name as long)
/// </summary>
public sealed class RemoveFriendDecoder : IPacketDecoder<RemoveFriendMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["RemoveFriend"];

    public RemoveFriendMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        return new RemoveFriendMessage(reader.ReadLong());
    }
}

/// <summary>
/// Decodes add ignore (opcode 61).
/// Legacy Java: readQWord (name as long)
/// </summary>
public sealed class AddIgnoreDecoder : IPacketDecoder<AddIgnoreMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["AddIgnore"];

    public AddIgnoreMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        return new AddIgnoreMessage(reader.ReadLong());
    }
}

/// <summary>
/// Decodes remove ignore (opcode 2).
/// Legacy Java: readQWord (name as long)
/// </summary>
public sealed class RemoveIgnoreDecoder : IPacketDecoder<RemoveIgnoreMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["RemoveIgnore"];

    public RemoveIgnoreMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        return new RemoveIgnoreMessage(reader.ReadLong());
    }
}

/// <summary>
/// Decodes private message send (opcode 178).
/// Legacy Java: readQWord (name), readUnsignedByte (numChars), decryptPlayerChat (text)
/// </summary>
public sealed class PrivateMessageDecoder : IPacketDecoder<PrivateMessageMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["PrivateMessage"];

    public PrivateMessageMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        long recipientLong = reader.ReadLong();
        int numChars = reader.ReadByte();
        var text = reader.ReadBytes(reader.Remaining).ToArray();
        return new PrivateMessageMessage(recipientLong, text);
    }
}
