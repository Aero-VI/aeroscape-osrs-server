using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

/// <summary>
/// Decodes public chat (opcode 222).
/// Legacy Java PublicChat: readUnsignedWord (chatTextEffects), readUnsignedByte (numChars), then decryptPlayerChat
/// </summary>
public sealed class PublicChatDecoder : IPacketDecoder<PublicChatMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["PublicChat"];

    public PublicChatMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int effects = reader.ReadUShort();
        int color = (effects >> 8) & 0xFF;
        int effect = effects & 0xFF;
        int numChars = reader.ReadByte();
        var text = reader.ReadBytes(reader.Remaining).ToArray();
        return new PublicChatMessage(color, effect, text);
    }
}

/// <summary>
/// Decodes command input (opcode 107).
/// Legacy Java Commands: readString
/// </summary>
public sealed class CommandDecoder : IPacketDecoder<CommandMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["Command"];

    public CommandMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        string full = reader.ReadString();
        var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new CommandMessage(
            parts.Length > 0 ? parts[0].ToLowerInvariant() : "",
            parts.Length > 1 ? parts[1..] : []
        );
    }
}

/// <summary>
/// Decodes clan chat join (opcode 42).
/// Legacy Java: readQWord (name as long)
/// </summary>
public sealed class JoinClanChatDecoder : IPacketDecoder<JoinClanChatMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["JoinClanChat"];

    public JoinClanChatMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        long clanNameLong = reader.ReadLong();
        return new JoinClanChatMessage(clanNameLong);
    }
}
