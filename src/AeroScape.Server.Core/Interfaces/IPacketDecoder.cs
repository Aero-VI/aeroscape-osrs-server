namespace AeroScape.Server.Core.Interfaces;

/// <summary>
/// Decodes raw packet bytes into a protocol-agnostic message record.
/// Implementations live in the Network layer and are keyed by packet name(s).
/// </summary>
public interface IPacketDecoder
{
    /// <summary>
    /// The packet names this decoder handles (from Protocol_508.json).
    /// Multiple names allow a single decoder to handle variants
    /// (e.g. Walk, WalkMinimap, WalkOnCommand).
    /// </summary>
    IReadOnlyList<string> PacketNames { get; }
}

/// <summary>
/// Typed decoder that produces a specific message struct from raw bytes.
/// </summary>
public interface IPacketDecoder<T> : IPacketDecoder where T : struct
{
    /// <summary>
    /// Decodes the raw packet payload into a protocol-agnostic message.
    /// The packet name is provided for decoders that handle multiple variants.
    /// </summary>
    T Decode(string packetName, ReadOnlySpan<byte> payload);
}
