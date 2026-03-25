using System.Net.Sockets;
using AeroScape.Server.Core.Crypto;
using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Interfaces;

namespace AeroScape.Server.Network.Session;

/// <summary>
/// Represents a connected client session. Holds the socket, ISAAC ciphers,
/// player state, and movement handler.
/// </summary>
public sealed class PlayerSession : IPlayerSession, IDisposable
{
    private readonly Socket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    public int SessionId { get; }
    public Player Player { get; }
    public MovementHandler Movement { get; } = new();
    
    public IsaacRandom? IncomingCipher { get; set; }
    public IsaacRandom? OutgoingCipher { get; set; }

    public bool IsConnected => !_disposed && _socket.Connected;
    
    /// <summary>Exposed for the connection pipeline's packet read loop.</summary>
    internal Socket Socket => _socket;

    public PlayerSession(int sessionId, Socket socket, Player player)
    {
        SessionId = sessionId;
        _socket = socket;
        Player = player;
    }

    public async ValueTask SendPacketAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (!IsConnected) return;
        
        await _sendLock.WaitAsync(ct);
        try
        {
            int sent = 0;
            while (sent < data.Length)
            {
                sent += await _socket.SendAsync(data[sent..], SocketFlags.None, ct);
            }
        }
        catch (Exception)
        {
            Disconnect("Send failed");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Disconnect(string reason = "")
    {
        if (_disposed) return;
        _disposed = true;
        
        try { _socket.Shutdown(SocketShutdown.Both); } catch { }
        try { _socket.Close(); } catch { }
    }

    public void Dispose()
    {
        Disconnect();
        _sendLock.Dispose();
        _socket.Dispose();
    }
}
