using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;

namespace AeroScape.Server.Network.Decoders;

public sealed class KeepAliveDecoder : IPacketDecoder<KeepAliveMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["KeepAlive"];

    public KeepAliveMessage Decode(string packetName, ReadOnlySpan<byte> data)
        => new();
}

public sealed class IdleLogoutDecoder : IPacketDecoder<IdleLogoutMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["IdleLogout"];

    public IdleLogoutMessage Decode(string packetName, ReadOnlySpan<byte> data)
        => new();
}

public sealed class RegionLoadedDecoder : IPacketDecoder<RegionLoadedMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["RegionLoaded", "RegionLoadedAlt"];

    public RegionLoadedMessage Decode(string packetName, ReadOnlySpan<byte> data)
        => new();
}

public sealed class FocusChangedDecoder : IPacketDecoder<FocusChangedMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["FocusChanged"];

    public FocusChangedMessage Decode(string packetName, ReadOnlySpan<byte> data)
        => new FocusChangedMessage(data.Length > 0 && data[0] == 1);
}

/// <summary>
/// Decodes camera moved / settings button (opcode 165).
/// Legacy Java: readDWord_v2 (junk)
/// </summary>
public sealed class CameraMovedDecoder : IPacketDecoder<CameraMovedMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["CameraMoved"];

    public CameraMovedMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int value = reader.ReadInt();
        int pitch = (value >> 16) & 0xFFFF;
        int yaw = value & 0xFFFF;
        return new CameraMovedMessage(pitch, yaw);
    }
}

/// <summary>
/// Decodes mouse click (opcode 59).
/// Legacy Java: readUnsignedWord, readDWord_v1
/// </summary>
public sealed class MouseClickDecoder : IPacketDecoder<MouseClickMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["MouseClick"];

    public MouseClickMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int info = reader.ReadUShort();
        int coords = reader.ReadInt();
        int x = (coords >> 16) & 0xFFFF;
        int y = coords & 0xFFFF;
        bool rightClick = (info & 1) != 0;
        return new MouseClickMessage(x, y, rightClick);
    }
}

/// <summary>
/// Decodes settings button clicks (opcode 165).
/// Legacy Java: readDWord_v2 — encodes setting ID + value.
/// </summary>
public sealed class SettingsButtonDecoder : IPacketDecoder<SettingsButtonMessage>
{
    public IReadOnlyList<string> PacketNames { get; } = ["SettingsButton"];

    public SettingsButtonMessage Decode(string packetName, ReadOnlySpan<byte> data)
    {
        var reader = new PacketReader(data);
        int value = reader.ReadInt();
        int settingId = (value >> 16) & 0xFFFF;
        int settingValue = value & 0xFFFF;
        return new SettingsButtonMessage(settingId, settingValue);
    }
}
