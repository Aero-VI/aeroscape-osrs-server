namespace AeroScape.Server.Core.Interfaces;

/// <summary>
/// Protocol-agnostic handler for a decoded game message.
/// Resolved from DI per-scope when a packet is decoded.
/// </summary>
public interface IMessageHandler<in TMessage> where TMessage : struct
{
    ValueTask HandleAsync(IPlayerSession session, TMessage message, CancellationToken ct = default);
}

/// <summary>
/// Abstraction over the network session — the Core layer sees this,
/// never the raw socket or pipeline.
/// </summary>
public interface IPlayerSession
{
    int SessionId { get; }
    Entities.Player Player { get; }
    
    ValueTask SendPacketAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    void Disconnect(string reason = "");
    bool IsConnected { get; }
}
