using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Basic combat system supporting player vs NPC melee combat.
/// Handles attack rolls, damage calculation, death, and respawning.
/// </summary>
public sealed class CombatSystem
{
    private readonly GameWorld _world;
    
    // Active combat targets: player index -> target NPC index
    private readonly Dictionary<int, int> _playerNpcTargets = new();
    
    // NPC respawn timers: npc index -> (ticks remaining, spawn npc id, spawn position)
    private readonly List<RespawnEntry> _respawnQueue = new();

    public CombatSystem(GameWorld world)
    {
        _world = world;
    }

    /// <summary>
    /// Sets a player to attack an NPC.
    /// </summary>
    public void AttackNpc(Player player, Npc npc)
    {
        _playerNpcTargets[player.Index] = npc.Index;
        player.FaceEntity(npc.Index);
    }

    /// <summary>
    /// Stops combat for a player.
    /// </summary>
    public void StopCombat(Player player)
    {
        _playerNpcTargets.Remove(player.Index);
    }

    /// <summary>
    /// Process one tick of combat for all engaged players.
    /// </summary>
    public void ProcessTick()
    {
        // Process player -> NPC combat
        var toRemove = new List<int>();

        foreach (var (playerIdx, npcIdx) in _playerNpcTargets)
        {
            var player = _world.GetPlayer(playerIdx);
            var npc = _world.GetNpc(npcIdx);

            if (player == null || !player.IsActive || npc == null || !npc.IsActive)
            {
                toRemove.Add(playerIdx);
                continue;
            }

            // Check distance
            if (!player.Position.WithinDistance(npc.Position, 1))
            {
                // Need to walk to target — handled by movement system
                continue;
            }

            // Attack every other tick (~1.2 seconds for standard melee)
            // Use a simple even/odd tick check based on player index
            // In a real server, this would track individual attack timers
            
            // Calculate hit
            int maxHit = CalculateMaxHit(player);
            int hit = Random.Shared.Next(maxHit + 1);
            
            // Apply accuracy check
            int attackRoll = CalculateAttackRoll(player);
            int defenceRoll = CalculateNpcDefenceRoll(npc);
            
            if (Random.Shared.Next(attackRoll + defenceRoll + 1) < defenceRoll)
                hit = 0; // Miss

            // Apply damage
            npc.HitDamage = hit;
            npc.HitType = hit > 0 ? 1 : 0; // 1 = normal, 0 = block
            npc.HitUpdateRequired = true;
            npc.UpdateRequired = true;

            npc.CurrentHealth = Math.Max(0, npc.CurrentHealth - hit);

            // NPC death
            if (npc.CurrentHealth <= 0)
            {
                // Death animation
                npc.PlayAnimation(836); // Generic death animation
                
                // Queue respawn
                _respawnQueue.Add(new RespawnEntry
                {
                    NpcId = npc.Id,
                    SpawnPosition = npc.SpawnPosition,
                    NpcName = npc.Name,
                    CombatLevel = npc.CombatLevel,
                    WalkRadius = npc.WalkRadius,
                    MaxHealth = npc.MaxHealth,
                    TicksRemaining = 50, // ~30 seconds
                    OriginalIndex = npc.Index
                });

                // Remove NPC from world (after this tick's update cycle)
                _world.UnregisterNpc(npc);
                toRemove.Add(playerIdx);

                // Give XP
                int xpGain = (int)(npc.MaxHealth * 4.0);
                player.Skills.AddExperience(0, xpGain); // Attack
                player.Skills.AddExperience(3, (int)(xpGain * 1.33)); // Hitpoints
            }
        }

        foreach (var idx in toRemove)
            _playerNpcTargets.Remove(idx);

        // Process respawns
        for (int i = _respawnQueue.Count - 1; i >= 0; i--)
        {
            var entry = _respawnQueue[i];
            entry.TicksRemaining--;

            if (entry.TicksRemaining <= 0)
            {
                // Respawn NPC
                var npc = new Npc(entry.NpcId, entry.SpawnPosition)
                {
                    Name = entry.NpcName,
                    CombatLevel = entry.CombatLevel,
                    WalkRadius = entry.WalkRadius,
                    CurrentHealth = entry.MaxHealth,
                    MaxHealth = entry.MaxHealth
                };
                npc.FacePosition(entry.SpawnPosition);
                _world.RegisterNpc(npc);
                _respawnQueue.RemoveAt(i);
            }
        }
    }

    private static int CalculateMaxHit(Player player)
    {
        int strengthLevel = player.Skills.GetLevel(2);
        // Simplified max hit formula
        return (int)(strengthLevel * 0.5 + 3);
    }

    private static int CalculateAttackRoll(Player player)
    {
        int attackLevel = player.Skills.GetLevel(0);
        // Simplified attack roll
        return attackLevel * 4 + 40;
    }

    private static int CalculateNpcDefenceRoll(Npc npc)
    {
        // Simplified NPC defence
        return npc.CombatLevel * 4 + 20;
    }

    private sealed class RespawnEntry
    {
        public int NpcId { get; set; }
        public Position SpawnPosition { get; set; }
        public string NpcName { get; set; } = "";
        public int CombatLevel { get; set; }
        public int WalkRadius { get; set; }
        public int MaxHealth { get; set; }
        public int TicksRemaining { get; set; }
        public int OriginalIndex { get; set; }
    }
}
