using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Skills;

/// <summary>
/// Mining skill — ported from legacy Java Mining.java.
/// Identical formulas, object-to-rock mappings, ore items, XP, level requirements,
/// pickaxe tiers, and animations.
/// Skill index: 14 (Mining).
/// </summary>
public sealed class MiningService
{
    private const int SkillId = 14;

    public sealed class MiningState
    {
        public bool Hacking { get; set; }
        public int RockId { get; set; }
        public int Animation { get; set; }
        public int Time { get; set; } = 4;
        public int OreTimer { get; set; } = -1;
        public int SecondTimer { get; set; } = 2;
    }

    // Pickaxe item IDs (from legacy getPlayerPickaxe, priority order)
    private static readonly int[] PickaxeIds = [1265, 1267, 1269, 1271, 1273, 1275];

    public static int GetPlayerPickaxe(Player player)
    {
        foreach (int pick in PickaxeIds)
        {
            if (player.Inventory.Contains(pick))
                return pick;
        }
        var weapon = player.Equipment.GetItem(3);
        if (weapon != null)
        {
            foreach (int pick in PickaxeIds)
            {
                if (weapon.Id == pick) return pick;
            }
        }
        return -1;
    }

    // Object ID → rock ID (from legacy getRockIDForObject)
    public static int GetRockIdForObject(int objectId) => objectId switch
    {
        2110 or 2090 or 11189 or 11190 or 11191 or 2091 => 1,  // Copper
        2094 or 11186 or 11187 or 11188 or 2095 => 2,          // Tin
        2092 or 2093 => 3,                                       // Iron
        2100 or 2101 => 4,                                       // Silver
        11183 or 11184 or 11185 or 2098 or 2099 => 6,           // Gold
        2096 or 2097 => 5,                                       // Coal
        2102 or 2103 => 7,                                       // Mithril
        2104 or 2105 => 8,                                       // Adamantite
        2106 or 2107 => 9,                                       // Rune
        4028 or 4029 or 4030 => 10,                              // Limestone
        6669 or 6670 or 6671 => 11,                              // Elemental
        16687 => 12,                                              // Rune essence
        _ => -1
    };

    // Rock ID → ore item ID (from legacy getOreForRock)
    public static int GetOreItemId(int rockId) => rockId switch
    {
        1 => 436,    // Copper
        2 => 438,    // Tin
        3 => 440,    // Iron
        4 => 442,    // Silver
        5 => 453,    // Coal
        6 => 444,    // Gold
        7 => 447,    // Mithril
        8 => 449,    // Adamantite
        9 => 451,    // Runite
        10 => 3211,  // Limestone
        11 => 2892,  // Elemental
        12 => 1436,  // Rune essence
        _ => -1
    };

    // Rock ID → XP per ore (from legacy getXpForOre)
    public static int GetXpForOre(int rockId) => rockId switch
    {
        1 => 50, 2 => 50, 3 or 12 => 75, 4 => 100,
        5 => 150, 6 => 240, 7 => 300, 8 => 400,
        9 => 600, 10 => 5, 11 => 20,
        _ => 0
    };

    // Rock ID → level required (from legacy getLevelForOre)
    public static int GetLevelRequired(int rockId) => rockId switch
    {
        1 or 12 => 1, 2 => 1, 3 => 15, 4 => 20,
        5 => 30, 6 => 40, 7 => 55, 8 => 70,
        9 => 80, 10 => 1, 11 => 10,
        _ => 1
    };

    // Pickaxe → animation (from legacy setAnimAndSpeed)
    public static int GetPickaxeAnimation(int pickaxeId) => pickaxeId switch
    {
        1265 => 625, 1267 => 626, 1269 => 627,
        1271 => 629, 1273 => 628, 1275 => 624,
        _ => 625
    };

    /// <summary>Begin mining a rock.</summary>
    public static MiningState? StartMining(Player player, int objectId)
    {
        int pick = GetPlayerPickaxe(player);
        if (pick == -1) return null;

        int rockId = GetRockIdForObject(objectId);
        if (rockId == -1) return null;

        if (GetLevelRequired(rockId) > player.Skills.GetLevel(SkillId))
            return null;

        int anim = GetPickaxeAnimation(pick);
        int time = 4;
        // From legacy: rune essence (rockId==12) → time = 1
        if (rockId == 12) time = 1;

        player.PlayAnimation(anim);

        return new MiningState
        {
            Hacking = true,
            RockId = rockId,
            Animation = anim,
            Time = time,
            OreTimer = time,
            SecondTimer = 2
        };
    }

    /// <summary>Process one tick of mining.</summary>
    public static void ProcessTick(Player player, MiningState state)
    {
        if (!state.Hacking) return;

        state.SecondTimer--;
        if (state.SecondTimer == 0)
        {
            state.SecondTimer = 2;
            state.OreTimer--;
            player.PlayAnimation(state.Animation);
        }

        if (state.OreTimer == 0)
        {
            state.OreTimer = state.Time;

            if (player.Inventory.FreeSlots < 1)
            {
                state.Hacking = false;
                state.OreTimer = -1;
                return;
            }

            int oreId = GetOreItemId(state.RockId);
            player.Inventory.Add(new Item(oreId, 1));
            // XP formula from legacy: (getXpForOre * skillLvl[14]) / 3
            int xp = GetXpForOre(state.RockId) * player.Skills.GetLevel(SkillId) / 3;
            player.Skills.AddExperience(SkillId, xp);
            player.PlayAnimation(state.Animation);
        }
    }
}
