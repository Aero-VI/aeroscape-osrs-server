using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Magic combat system — ported from legacy Java MagicNPC.java.
/// Implements spell casting on NPCs with rune requirements, level checks,
/// damage formulas, GFX, and experience.
/// </summary>
public sealed class MagicSystem
{
    private const int MagicSkillId = 6;
    private const int MagicXpRate = 5;

    // Rune item IDs (from legacy)
    public const int Fire = 554, Water = 555, Air = 556, Earth = 557;
    public const int Mind = 558, Body = 559, Law = 563, Cosmic = 564;
    public const int Death = 560, Nature = 561, Chaos = 562;
    public const int Blood = 565, Soul = 566;

    /// <summary>Maps button ID to internal spell ID (from legacy getSpellId).</summary>
    public static int GetSpellId(int buttonId) => buttonId switch
    {
        129 => 1,  // wind strike
        132 => 2,  // water strike
        134 => 3,  // earth strike
        136 => 4,  // fire strike
        138 => 5,  // wind bolt
        142 => 6,  // water bolt
        145 => 7,  // earth bolt
        148 => 8,  // fire bolt
        152 => 9,  // wind blast
        155 => 10, // water blast
        161 => 11, // earth blast
        166 => 12, // fire blast
        173 => 13, // wind wave
        176 => 14, // water wave
        180 => 15, // earth wave
        183 => 16, // fire wave
        _ => -1
    };

    /// <summary>Required magic level per spell (from legacy getLevelForSpell).</summary>
    public static int GetLevelForSpell(int spellId) => spellId switch
    {
        1 => 1, 2 => 5, 3 => 9, 4 => 13,
        5 => 17, 6 => 23, 7 => 29, 8 => 35,
        9 => 41, 10 => 47, 11 => 53, 12 => 59,
        13 => 62, 14 => 65, 15 => 70, 16 => 75,
        _ => -1
    };

    /// <summary>Base XP per spell (from legacy getExpForSpell).</summary>
    public static double GetBaseXp(int spellId) => spellId switch
    {
        1 => 25, 2 => 35, 3 => 60, 4 => 80,
        5 => 110, 6 => 140, 7 => 170, 8 => 200,
        9 => 215, 10 => 220, 11 => 235, 12 => 250,
        13 => 350, 14 => 360, 15 => 380, 16 => 450,
        _ => 0
    };

    /// <summary>Max hit per spell (from legacy getMaxHit).</summary>
    public static int GetMaxHit(int spellId)
    {
        int maxHit = 0;
        for (int i = 1; i <= spellId; i++)
            maxHit += (i <= 4) ? 2 : 1;
        return maxHit;
    }

    /// <summary>Equipment bonus damage (from legacy getBonusDamage).</summary>
    public static int GetBonusDamage(Player player, int magicBonus)
    {
        double c = player.Skills.GetLevel(MagicSkillId);
        double d = magicBonus;
        double f = (d * 0.00175) + 0.1;
        double h = Math.Floor(c * f + 1.06) / 4;
        return (int)h;
    }

    /// <summary>Caster GFX per spell (from legacy getPlayerGFX).</summary>
    public static int GetCasterGfx(int spellId) => spellId switch
    {
        1 => 90, 2 => 93, 3 => 96, 4 => 99,
        5 => 117, 6 => 120, 7 => 123, 8 => 126,
        9 => 132, 10 => 135, 11 => 138, 12 => 129,
        13 => 158, 14 => 161, 15 => 164, 16 => 155,
        _ => -1
    };

    /// <summary>Victim GFX per spell (from legacy getNpcGFX = playerGFX + 2).</summary>
    public static int GetVictimGfx(int spellId) => GetCasterGfx(spellId) + 2;

    /// <summary>Get rune requirements for a spell (from legacy getRunes).</summary>
    public static (int RuneId, int Amount)[] GetRuneRequirements(int spellId, int weaponId = -1)
    {
        var reqs = spellId switch
        {
            1 => new[] { (Air, 1), (Mind, 1) },
            2 => new[] { (Water, 1), (Air, 1), (Mind, 1) },
            3 => new[] { (Earth, 2), (Air, 1), (Mind, 1) },
            4 => new[] { (Fire, 3), (Air, 2), (Mind, 1) },
            5 => new[] { (Air, 2), (Chaos, 1) },
            6 => new[] { (Water, 2), (Air, 2), (Chaos, 1) },
            7 => new[] { (Earth, 3), (Air, 2), (Chaos, 1) },
            8 => new[] { (Fire, 4), (Air, 3), (Chaos, 1) },
            9 => new[] { (Air, 3), (Death, 1) },
            10 => new[] { (Water, 3), (Air, 3), (Death, 1) },
            11 => new[] { (Earth, 4), (Air, 3), (Death, 1) },
            12 => new[] { (Fire, 5), (Air, 4), (Death, 1) },
            13 => new[] { (Air, 5), (Blood, 1) },
            14 => new[] { (Water, 7), (Air, 5), (Blood, 1) },
            15 => new[] { (Earth, 7), (Air, 5), (Blood, 1) },
            16 => new[] { (Fire, 7), (Air, 5), (Blood, 1) },
            _ => Array.Empty<(int, int)>()
        };

        // Staff check (from legacy checkStaff) — elemental staves remove rune requirement
        int staffElement = weaponId switch
        {
            1381 => Air,
            1383 => Water,
            1385 => Earth,
            1387 => Fire,
            _ => -1
        };

        if (staffElement == -1) return reqs;
        return reqs.Where(r => r.Item1 != staffElement).ToArray();
    }

    /// <summary>Check if player has a staff equipped (from legacy hasStaff).</summary>
    public static bool HasStaff(int weaponId) => weaponId is 1379 or 1381 or 1383 or 1385 or 1387;

    /// <summary>Check if player has required runes.</summary>
    public static bool HasRunes(Player player, (int RuneId, int Amount)[] requirements)
    {
        foreach (var (runeId, amount) in requirements)
        {
            if (!player.Inventory.Contains(runeId, amount))
                return false;
        }
        return true;
    }

    /// <summary>Remove runes from inventory.</summary>
    public static void ConsumeRunes(Player player, (int RuneId, int Amount)[] requirements)
    {
        foreach (var (runeId, amount) in requirements)
            player.Inventory.RemoveById(runeId, amount);
    }

    /// <summary>Calculate final XP for a spell cast (from legacy getExpByHit).</summary>
    public static int CalculateXp(int spellId, int damage)
    {
        return (int)((GetBaseXp(spellId) + damage) * MagicXpRate);
    }

    // ── Ancient magicks data (from legacy Magic.java) ──

    public static readonly int[] AncientLevelReq =
    [
        58, 82, 70, 94, 56, 80, 68, 92, 50, 74, 62, 86, 52, 76, 64, 88,
        54, 60, 66, 72, 78, 84, 90, 96, 0
    ];

    public static readonly int[] AncientSpellXp =
    [
        34, 46, 40, 52, 33, 45, 39, 51, 30, 42, 36, 48, 31, 43, 37, 49,
        64, 70, 76, 82, 88, 94, 100, 106, 0
    ];

    public static readonly int[] AncientMaxHit =
    [
        18, 26, 22, 30, 17, 25, 21, 29, 15, 23, 19, 27, 16, 24, 20, 28,
        0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    // Modern magic data (from legacy Magic.java)
    public static readonly int[] ModernLevelReq =
    [
        0, 1, 3, 4, 5, 7, 9, 11, 13, 15, 17, 19, 20, 21, 23, 25, 27, 29, 31, 32,
        33, 35, 37, 39, 40, 41, 43, 45, 47, 49, 50, 50, 50, 51, 53, 55, 56, 57,
        58, 59, 60, 60, 60, 60, 60, 61, 62, 63, 64, 65, 66, 66, 68, 70, 73, 74,
        75, 79, 80, 80, 82, 87, 90
    ];

    public static readonly int[] ModernMaxHit =
    [
        0, 2, 0, 0, 4, 0, 6, 0, 8, 0, 9, 0, 0, 0, 10, 0, 0, 11, 0, 0, 12, 0, 15,
        0, 13, 0, 0, 14, 25, 2, 19, 19, 0, 15, 0, 0, 0, 0, 16, 0, 0, 20, 20, 20,
        0, 17, 0, 0, 18, 0, 0, 0, 19, 0, 0, 20, 0, 0, 0, 0, 0, 0
    ];
}
