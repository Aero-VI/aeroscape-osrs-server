using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Skills;

/// <summary>
/// Woodcutting skill — ported from legacy Java Woodcutting.java.
/// Identical formulas, timers, animations, axe tiers, tree data.
/// Skill index: 8 (Woodcutting).
/// </summary>
public sealed class WoodcuttingService
{
    private const int SkillId = 8;

    public sealed class WoodcuttingState
    {
        public bool Chopping { get; set; }
        public int TreeId { get; set; }
        public int Animation { get; set; }
        public int Time { get; set; } = 4;
        public int LogTimer { get; set; } = -1;
        public int SecondTimer { get; set; } = 2;
        public int Logs { get; set; }
        public int MaxLogs { get; set; }
    }

    // Object ID → internal tree ID (from legacy getTreeIDForObject)
    public static int GetTreeIdForObject(int objectId) => objectId switch
    {
        1277 or 1278 or 1276 => 1, // Normal
        1281 => 2, // Oak
        1308 => 3, // Willow
        9036 => 4, // Teak
        1307 => 5, // Maple
        9034 => 6, // Mahogany
        1309 => 7, // Yew
        1306 => 8, // Magic
        _ => -1
    };

    // Tree ID → log item ID
    public static int GetLogItemId(int treeId) => treeId switch
    {
        1 => 1511, 2 => 1521, 3 => 1519, 4 => 6333,
        5 => 1517, 6 => 6332, 7 => 1515, 8 => 1513,
        _ => -1
    };

    // Tree ID → XP per log
    public static int GetXpForLog(int treeId) => treeId switch
    {
        1 => 50, 2 => 75, 3 => 100, 4 => 150,
        5 => 175, 6 => 250, 7 => 300, 8 => 500,
        _ => 0
    };

    // Tree ID → level required
    public static int GetLevelRequired(int treeId) => treeId switch
    {
        1 => 1, 2 => 15, 3 => 30, 4 => 35,
        5 => 45, 6 => 50, 7 => 60, 8 => 75,
        _ => 1
    };

    // Tree ID → max logs from tree
    public static int GetMaxLogs(int treeId) => treeId switch
    {
        1 => 1, 2 => 2, 3 => 3, 4 => 4,
        5 => 5, 6 => 5, 7 => 4, 8 => 8,
        _ => 1
    };

    // Axe item IDs in priority order (from legacy getPlayerAxe)
    private static readonly int[] AxeIds = [1351, 1349, 1353, 1361, 1355, 1357, 1359, 6739];

    public static int GetPlayerAxe(Player player)
    {
        foreach (int axe in AxeIds)
        {
            if (player.Inventory.Contains(axe))
                return axe;
        }
        // Check weapon slot (equipment[3])
        var weapon = player.Equipment.GetItem(3);
        if (weapon != null)
        {
            foreach (int axe in AxeIds)
            {
                if (weapon.Id == axe) return axe;
            }
        }
        return -1;
    }

    // Axe → animation (from legacy setAnimAndSpeed)
    public static int GetAxeAnimation(int axeId) => axeId switch
    {
        1351 => 879, 1349 => 877, 1353 => 875, 1361 => 873,
        1355 => 871, 1357 => 869, 1359 => 867, 6739 => 2846,
        _ => 879
    };

    /// <summary>Begin cutting a tree.</summary>
    public static WoodcuttingState? StartCutting(Player player, int objectId)
    {
        int axe = GetPlayerAxe(player);
        if (axe == -1) return null; // "You don't have an axe..."

        int treeId = GetTreeIdForObject(objectId);
        if (treeId == -1) return null;

        if (GetLevelRequired(treeId) > player.Skills.GetLevel(SkillId))
            return null; // "You need level X to chop down this tree"

        int anim = GetAxeAnimation(axe);
        int maxLogs = GetMaxLogs(treeId);
        if (maxLogs == 0) maxLogs = 1;

        player.PlayAnimation(anim);

        return new WoodcuttingState
        {
            Chopping = true,
            TreeId = treeId,
            Animation = anim,
            Time = 4, // From legacy: time always = 4
            LogTimer = 4,
            SecondTimer = 2,
            MaxLogs = maxLogs
        };
    }

    /// <summary>Process one tick of woodcutting.</summary>
    public static void ProcessTick(Player player, WoodcuttingState state)
    {
        if (!state.Chopping) return;

        state.SecondTimer--;
        if (state.SecondTimer == 0)
        {
            state.SecondTimer = 2;
            state.LogTimer--;
            player.PlayAnimation(state.Animation);
        }

        if (state.LogTimer == 0)
        {
            state.LogTimer = state.Time;
            state.Logs++;

            // doLog from legacy
            if (player.Inventory.FreeSlots < 1)
            {
                state.Chopping = false;
                state.LogTimer = -1;
                return; // "Not enough inventory space"
            }

            int logId = GetLogItemId(state.TreeId);
            player.Inventory.Add(new Item(logId, 1));
            // XP formula from legacy: (getXpForLog * skillLvl[8]) / 3
            int xp = GetXpForLog(state.TreeId) * player.Skills.GetLevel(SkillId) / 3;
            player.Skills.AddExperience(SkillId, xp);
            player.PlayAnimation(state.Animation);
        }
    }
}
