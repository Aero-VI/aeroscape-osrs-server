using System.Text.Json;
using AeroScape.Server.Core.Entities;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Loads NPC spawns from a JSON file and registers them in the world.
/// </summary>
public sealed class NpcSpawnLoader
{
    private readonly GameWorld _world;
    private readonly ILogger<NpcSpawnLoader> _logger;

    public NpcSpawnLoader(GameWorld world, ILogger<NpcSpawnLoader> logger)
    {
        _world = world;
        _logger = logger;
    }

    public async Task LoadAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("NPC spawn file not found: {Path}. No NPCs loaded.", filePath);
            return;
        }

        await using var stream = File.OpenRead(filePath);
        var spawns = await JsonSerializer.DeserializeAsync<NpcSpawnEntry[]>(stream, cancellationToken: ct);

        if (spawns == null) return;

        int count = 0;
        foreach (var spawn in spawns)
        {
            var npc = new Npc(spawn.Id, new Position(spawn.X, spawn.Y, spawn.Z))
            {
                Name = spawn.Name ?? $"NPC-{spawn.Id}",
                CombatLevel = spawn.CombatLevel,
                WalkRadius = spawn.WalkRadius,
                CurrentHealth = spawn.MaxHealth,
                MaxHealth = spawn.MaxHealth
            };
            npc.FacePosition(new Position(spawn.FaceX, spawn.FaceY));

            if (_world.RegisterNpc(npc) != -1)
                count++;
        }

        _logger.LogInformation("Loaded {Count} NPC spawns from {Path}", count, filePath);
    }
}

public sealed class NpcSpawnEntry
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int FaceX { get; set; }
    public int FaceY { get; set; }
    public int CombatLevel { get; set; }
    public int WalkRadius { get; set; }
    public int MaxHealth { get; set; } = 100;
}
