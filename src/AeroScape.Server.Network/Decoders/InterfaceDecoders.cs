using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

/// <summary>
/// Decodes button clicks (opcodes 233, 113, 21, 169, 232, 173).
/// Legacy Java ActionButtons: typically reads interface hash and button data.
/// </summary>
public sealed class ButtonClickDecoder : IPacketDecoder<ButtonClickMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = [
        "ButtonClick1", "ButtonClick2", "ButtonClick3",
        "ButtonClick4", "ButtonClick5", "ButtonClick6"
    ];

    public ButtonClickMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int interfaceHash = reader.ReadInt();
        int interfaceId = interfaceHash >> 16;
        int buttonId = interfaceHash & 0xFFFF;
        // Remaining 2 bytes may be item slot/id — ignored at decode level
        return new ButtonClickMessage(interfaceId, buttonId);
    }
}

/// <summary>
/// Decodes dialogue continue (opcode 63) — empty payload in 508.
/// Legacy Java: handled inline in PacketManager case 63 with no stream reads for the basic case.
/// </summary>
public sealed class DialogueContinueDecoder : IPacketDecoder<DialogueContinueMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["DialogueContinue"];

    public DialogueContinueMessage Decode(string packetName, ReadOnlySpan<byte> data)
        => new DialogueContinueMessage(0, 0);
}

/// <summary>
/// Decodes close interface (opcode 108).
/// Legacy Java: no payload read, triggers UI state reset.
/// </summary>
public sealed class CloseInterfaceDecoder : IPacketDecoder<CloseInterfaceMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["CloseInterface"];

    public CloseInterfaceMessage Decode(string packetName, ReadOnlySpan<byte> data)
        => new();
}

/// <summary>
/// Decodes appearance update (opcode 101).
/// Legacy Java: reads gender byte, 7 look bytes, 5 color bytes.
/// </summary>
public sealed class AppearanceUpdateDecoder : IPacketDecoder<AppearanceUpdateMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["AppearanceUpdate"];

    public AppearanceUpdateMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int gender = reader.ReadByte();
        var look = new int[7];
        for (int i = 0; i < 7; i++)
            look[i] = reader.ReadByte();
        var colors = new int[5];
        for (int i = 0; i < 5; i++)
            colors[i] = reader.ReadByte();
        return new AppearanceUpdateMessage(gender, look, colors);
    }
}
