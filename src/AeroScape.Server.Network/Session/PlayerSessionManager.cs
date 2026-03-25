using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Session;

/// <summary>
/// Singleton managing all active PlayerSessions.
/// Thread-safe via ConcurrentDictionary.
/// </summary>
public sealed class PlayerSessionManager
{
    private readonly ConcurrentDictionary<int, PlayerSession> _sessions = new();
    private readonly ILogger<PlayerSessionManager> _logger;
    private int _nextId;

    public PlayerSessionManager(ILogger<PlayerSessionManager> logger)
    {
        _logger = logger;
    }

    public int Count => _sessions.Count;

    public int NextSessionId() => Interlocked.Increment(ref _nextId);

    public void Register(PlayerSession session)
    {
        _sessions[session.SessionId] = session;
        _logger.LogDebug("Session {Id} registered ({Count} active)", session.SessionId, Count);
    }

    public void Unregister(PlayerSession session)
    {
        _sessions.TryRemove(session.SessionId, out _);
        _logger.LogDebug("Session {Id} unregistered ({Count} active)", session.SessionId, Count);
    }

    public PlayerSession? Get(int sessionId) =>
        _sessions.GetValueOrDefault(sessionId);

    public IEnumerable<PlayerSession> GetAll() => _sessions.Values;
}
