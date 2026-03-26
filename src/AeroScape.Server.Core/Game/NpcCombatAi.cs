using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// NPC combat AI — ported from legacy Java NPCPlayerCombat.java.
/// Handles NPCs attacking players, including dragon fire attacks,
/// prayer protection, auto-retaliate, and NPC-specific attack animations.
/// </summary>
public sealed class NpcCombatAi
{
    private readonly GameWorld _world;

    // Active NPC→Player targets: npc index → target player index
    private readonly Dictionary<int, int> _npcTargets = new();

    public NpcCombatAi(GameWorld world)
    {
        _world = world;
    }

    /// <summary>Set an NPC to attack a player (from legacy n.attackPlayer = p.playerId).</summary>
    public void SetTarget(Npc npc, Player player)
    {
        _npcTargets[npc.Index] = player.Index;
    }

    /// <summary>Stop NPC combat.</summary>
    public void ResetAttack(Npc npc)
    {
        _npcTargets.Remove(npc.Index);
    }

    /// <summary>Process one tick of NPC → Player combat.</summary>
    public void ProcessTick()
    {
        var toRemove = new List<int>();

        foreach (var (npcIdx, playerIdx) in _npcTargets)
        {
            var npc = _world.GetNpc(npcIdx);
            var player = _world.GetPlayer(playerIdx);

            if (npc == null || !npc.IsActive || player == null || !player.IsActive || player.IsDead)
            {
                toRemove.Add(npcIdx);
                continue;
            }

            // Check distance — NPC must be within 1 tile (melee range)
            if (!npc.Position.WithinDistance(player.Position, 1))
                continue;

            // Combat delay check
            // (NPC combat delay is tracked via the Npc entity in a full impl)

            // Face player (from legacy: n.requestFaceTo(p.playerId + 32768))
            npc.FaceEntity(player.Index + 32768);

            // Determine max hit based on NPC combat level
            int maxHit = Math.Max(1, npc.CombatLevel / 5);
            int hitDamage = Random.Shared.Next(maxHit + 1);

            // Dragon NPC special attacks (from legacy — NPC types 742, 5363, 55, 53, 941)
            bool isDragon = npc.Id is 742 or 5363 or 55 or 53 or 941;
            if (isDragon && Random.Shared.Next(2) == 1)
            {
                // Dragon fire attack
                npc.PlayGraphic(1);
                npc.PlayAnimation(81);
                
                // Check for anti-dragon shield (slot 5 = shield)
                var shield = player.Equipment.GetItem(5);
                if (shield != null && (shield.Id == 1540 || shield.Id == 11283))
                {
                    hitDamage = Random.Shared.Next(6); // Reduced damage
                }
                else
                {
                    hitDamage = 10 + Random.Shared.Next(21); // Full dragon fire
                }
            }
            else
            {
                // Standard melee attack with NPC's attack animation
                // Use generic animation 422 for humanoids, 451 for animals
                int attackAnim = npc.Id switch
                {
                    9 or 21 or 20 => 451,     // Animals
                    2 or 1 => 422,             // Humanoids
                    _ => 422                    // Default
                };
                npc.PlayAnimation(attackAnim);
            }

            // Protection prayer check (from legacy: prayerIcon == 0 → melee protect)
            if (player.PrayerIcon == 0) // Protect from melee
            {
                hitDamage = 0; // Blocked by prayer
            }

            // Apply hit to player
            player.HitDamage = hitDamage;
            player.HitType = hitDamage > 0 ? 1 : 0;
            player.HitUpdateRequired = true;
            player.UpdateRequired = true;
            player.PlayAnimation(424); // Defend animation

            // Reduce player hitpoints
            int currentHp = player.Skills.GetLevel(3);
            player.Skills.SetLevel(3, Math.Max(0, currentHp - hitDamage));

            // Auto-retaliate (from legacy: if autoRetaliate == 0 && !attackingNPC)
            if (player.AutoRetaliate && player.FollowTargetIndex == null)
            {
                player.FollowTargetIndex = npc.Index;
                player.CombatDelay += 3;
            }

            // Player death
            if (player.Skills.GetLevel(3) <= 0)
            {
                player.IsDead = true;
                player.PlayAnimation(836); // Death animation
                toRemove.Add(npcIdx);
            }
        }

        foreach (var idx in toRemove)
            _npcTargets.Remove(idx);
    }
}
