using System.Collections.Concurrent;
using AeroScape.Server.Core.Entities;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Manages all active players and NPCs. Singleton.
/// </summary>
public sealed class GameWorld
{
    private readonly Player?[] _players = new Player?[Constants.ServerConstants.MaxPlayers];
    private readonly ConcurrentDictionary<string, int> _usernameIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<GameWorld> _logger;

    public GameWorld(ILogger<GameWorld> logger)
    {
        _logger = logger;
    }

    public int PlayerCount => _usernameIndex.Count;

    public Player? GetPlayer(int index) =>
        index >= 0 && index < _players.Length ? _players[index] : null;

    public bool IsOnline(string username) =>
        _usernameIndex.ContainsKey(username);

    public int Register(Player player)
    {
        for (int i = 1; i < _players.Length; i++)
        {
            if (_players[i] is null)
            {
                _players[i] = player;
                player.Index = i;
                player.IsActive = true;
                _usernameIndex[player.Username] = i;
                _logger.LogInformation("Player {Username} registered at index {Index} ({Count} online)",
                    player.Username, i, PlayerCount);
                return i;
            }
        }
        return -1; // World full
    }

    public void Unregister(Player player)
    {
        if (player.Index < 0) return;
        _players[player.Index] = null;
        _usernameIndex.TryRemove(player.Username, out _);
        player.IsActive = false;
        _logger.LogInformation("Player {Username} unregistered ({Count} online)",
            player.Username, PlayerCount);
        player.Index = -1;
    }

    /// <summary>
    /// Returns all active players (for update cycle).
    /// </summary>
    public IEnumerable<Player> GetActivePlayers()
    {
        for (int i = 1; i < _players.Length; i++)
        {
            if (_players[i] is { IsActive: true } p)
                yield return p;
        }
    }
}
