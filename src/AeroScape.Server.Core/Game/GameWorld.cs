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
    private readonly Npc?[] _npcs = new Npc?[Constants.ServerConstants.MaxNpcs];
    private readonly ConcurrentDictionary<string, int> _usernameIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<GameWorld> _logger;

    // Ground items: key = packed position (x << 16 | y << 2 | z), value = list of ground items
    private readonly ConcurrentDictionary<long, List<GroundItem>> _groundItems = new();

    public GameWorld(ILogger<GameWorld> logger)
    {
        _logger = logger;
    }

    public int PlayerCount => _usernameIndex.Count;
    public int NpcCount { get; private set; }

    public Player? GetPlayer(int index) =>
        index >= 0 && index < _players.Length ? _players[index] : null;

    public Npc? GetNpc(int index) =>
        index >= 0 && index < _npcs.Length ? _npcs[index] : null;

    public bool IsOnline(string username) =>
        _usernameIndex.ContainsKey(username);

    public Player? FindPlayer(string username) =>
        _usernameIndex.TryGetValue(username, out var idx) ? _players[idx] : null;

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

    public int RegisterNpc(Npc npc)
    {
        for (int i = 1; i < _npcs.Length; i++)
        {
            if (_npcs[i] is null)
            {
                _npcs[i] = npc;
                npc.Index = i;
                npc.IsActive = true;
                NpcCount++;
                return i;
            }
        }
        return -1;
    }

    public void UnregisterNpc(Npc npc)
    {
        if (npc.Index < 0) return;
        _npcs[npc.Index] = null;
        npc.IsActive = false;
        NpcCount--;
        npc.Index = -1;
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

    /// <summary>
    /// Returns all active NPCs.
    /// </summary>
    public IEnumerable<Npc> GetActiveNpcs()
    {
        for (int i = 1; i < _npcs.Length; i++)
        {
            if (_npcs[i] is { IsActive: true } n)
                yield return n;
        }
    }

    // --- Ground Items ---

    private static long PackPosition(int x, int y, int z) => ((long)x << 16) | ((long)y << 2) | (long)z;

    public void AddGroundItem(GroundItem item)
    {
        var key = PackPosition(item.Position.X, item.Position.Y, item.Position.Z);
        var list = _groundItems.GetOrAdd(key, _ => new List<GroundItem>());
        lock (list)
        {
            list.Add(item);
        }
    }

    public GroundItem? RemoveGroundItem(int itemId, Position position, string? owner = null)
    {
        var key = PackPosition(position.X, position.Y, position.Z);
        if (!_groundItems.TryGetValue(key, out var list)) return null;
        lock (list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var gi = list[i];
                if (gi.ItemId == itemId && (owner == null || gi.Owner == owner))
                {
                    list.RemoveAt(i);
                    return gi;
                }
            }
        }
        return null;
    }

    public IReadOnlyList<GroundItem> GetGroundItems(Position position)
    {
        var key = PackPosition(position.X, position.Y, position.Z);
        if (!_groundItems.TryGetValue(key, out var list)) return [];
        lock (list)
        {
            return list.ToList();
        }
    }

    public IEnumerable<GroundItem> GetGroundItemsInRegion(Position center, int distance = 32)
    {
        // Scan all ground items within distance
        foreach (var (_, list) in _groundItems)
        {
            List<GroundItem> snapshot;
            lock (list) { snapshot = list.ToList(); }
            foreach (var gi in snapshot)
            {
                if (gi.Position.WithinDistance(center, distance))
                    yield return gi;
            }
        }
    }

    public void TickGroundItems()
    {
        foreach (var (key, list) in _groundItems)
        {
            lock (list)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var gi = list[i];
                    gi.TicksRemaining--;
                    if (gi.TicksRemaining <= 0)
                    {
                        list.RemoveAt(i);
                    }
                    else if (gi.TicksRemaining <= gi.PublicAfterTicks && !gi.IsPublic)
                    {
                        gi.IsPublic = true;
                    }
                }
            }
        }
    }
}

/// <summary>
/// A ground item in the world.
/// </summary>
public sealed class GroundItem
{
    public int ItemId { get; set; }
    public int Amount { get; set; }
    public Position Position { get; set; }
    public string? Owner { get; set; } // username — null = public
    public bool IsPublic { get; set; }
    public int TicksRemaining { get; set; } = 200; // ~2 minutes
    public int PublicAfterTicks { get; set; } = 100; // becomes public after ~1 minute

    public GroundItem(int itemId, int amount, Position position, string? owner = null)
    {
        ItemId = itemId;
        Amount = amount;
        Position = position;
        Owner = owner;
    }
}
