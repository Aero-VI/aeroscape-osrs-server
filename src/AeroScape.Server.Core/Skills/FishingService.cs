using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Skills;

/// <summary>
/// Fishing skill — ported from legacy Java Fishing.java.
/// Identical formulas, timers, animations, and bait consumption.
/// Skill index: 10 (Fishing).
/// </summary>
public sealed class FishingService
{
    private const int SkillId = 10;
    private static readonly Random Rng = Random.Shared;

    public record FishingState
    {
        public bool Active { get; set; }
        public int NetType { get; set; }   // 1=net, 2=bait rod, 3=fly rod, 4=harpoon
        public int FishItemId { get; set; }
        public int FishXp { get; set; }
        public int Timer { get; set; }
        public bool NeedsBait { get; set; }
    }

    // Net type → animation id (from legacy)
    public static int GetAnimation(int netType) => netType switch
    {
        1 => 620,
        2 => 622,
        3 => 619,
        4 => 618,
        _ => 620
    };

    /// <summary>Process one tick for a player's fishing state.</summary>
    public static void ProcessTick(Player player, FishingState state)
    {
        if (!state.Active) return;

        if (state.Timer > 0)
        {
            state.Timer--;
            return;
        }

        // Timer reached 0 — attempt to catch
        player.PlayAnimation(GetAnimation(state.NetType));

        if (state.NeedsBait && state.NetType == 2)
        {
            // Bait rod requires bait item 313
            if (!player.Inventory.Contains(313))
            {
                state.Active = false;
                state.NetType = 0;
                // Would send message: "You need more fishing bait!"
                return;
            }
            player.Inventory.RemoveById(313, 1);
        }

        player.Inventory.Add(new Item(state.FishItemId, 1));
        // XP formula from legacy: (FishXP * skillLvl[10]) / 3
        int xp = state.FishXp * player.Skills.GetLevel(SkillId) / 3;
        player.Skills.AddExperience(SkillId, xp);

        // Reset timer: 4 + random(6) ticks — identical to legacy
        state.Timer = 4 + Rng.Next(7);
    }
}
