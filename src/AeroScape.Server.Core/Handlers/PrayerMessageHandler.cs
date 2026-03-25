using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles prayer toggle requests — activates/deactivates prayers, manages
/// conflicting prayers, overhead icons, and drain rates.
/// Translated from legacy Java: DavidScape.io.packets.Prayer
/// </summary>
public sealed class PrayerMessageHandler : IMessageHandler<PrayerMessage>
{
    // Config IDs sent to the client to toggle prayer orbs on/off
    private static readonly int[] PrayerConfig =
    {
        83, 84, 85, 862, 863, 86, 87, 88, 89, 90,
        91, 864, 865, 92, 93, 94, 95, 96, 97, 866,
        867, 98, 99, 100, 1168, 1052, 1053
    };

    // Required prayer level for each prayer index
    private static readonly int[] PrayerLevel =
    {
        1, 4, 7, 8, 9, 10, 13, 16, 19, 22,
        25, 26, 27, 28, 31, 34, 37, 40, 43, 44,
        45, 46, 49, 52, 35, 60, 70
    };

    // Prayer point drain rate per prayer
    private static readonly int[] DrainRate =
    {
        3, 4, 5, 6, 7, 8, 9, 10, 6, 7,
        6, 12, 13, 14, 15, 16, 17, 18, 19, 20,
        21, 22, 23, 24, 15, 26, 28
    };

    // Prayers that conflict with each other, indexed by prayer slot
    private static readonly int[][] ConflictTable =
    {
        /* 0  */ new[] { 5, 13, 25, 26 },
        /* 1  */ new[] { 3, 4, 6, 11, 12, 14, 19, 20, 25, 26 },
        /* 2  */ new[] { 3, 4, 7, 11, 12, 15, 19, 20, 25, 26 },
        /* 3  */ new[] { 1, 2, 4, 6, 7, 11, 12, 14, 15, 19, 20, 25, 26 },
        /* 4  */ new[] { 1, 2, 3, 6, 7, 11, 12, 14, 15, 19, 20, 25, 26 },
        /* 5  */ new[] { 0, 13, 25, 26 },
        /* 6  */ new[] { 1, 3, 4, 11, 12, 14, 19, 20, 25, 26 },
        /* 7  */ new[] { 2, 3, 4, 11, 12, 15, 19, 20, 25, 26 },
        /* 8  */ Array.Empty<int>(),
        /* 9  */ Array.Empty<int>(),
        /* 10 */ Array.Empty<int>(),
        /* 11 */ new[] { 1, 2, 3, 4, 6, 7, 12, 14, 15, 19, 20, 25, 26 },
        /* 12 */ new[] { 1, 2, 3, 4, 6, 7, 11, 14, 15, 19, 20, 25, 26 },
        /* 13 */ new[] { 0, 5, 25, 26 },
        /* 14 */ new[] { 1, 3, 4, 6, 11, 12, 19, 20, 25, 26 },
        /* 15 */ new[] { 2, 3, 4, 7, 11, 12, 19, 20, 25, 26 },
        /* 16 */ new[] { 17, 18, 21, 22, 23, 24 },
        /* 17 */ new[] { 16, 18, 21, 22, 23, 24 },
        /* 18 */ new[] { 16, 17, 21, 22, 23, 24 },
        /* 19 */ new[] { 1, 2, 3, 4, 6, 7, 11, 12, 14, 15, 20, 25, 26 },
        /* 20 */ new[] { 1, 2, 3, 4, 6, 7, 11, 12, 14, 15, 19, 25, 26 },
        /* 21 */ new[] { 16, 17, 18, 22, 23, 24 },
        /* 22 */ new[] { 16, 17, 18, 21, 23, 24 },
        /* 23 */ new[] { 16, 17, 18, 21, 22, 24 },
        /* 24 */ new[] { 16, 17, 18, 21, 22, 23 },
        /* 25 */ new[] { 0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15, 19, 20, 26 },
        /* 26 */ new[] { 0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15, 19, 20, 25 },
    };

    // Overhead prayer icon per prayer index (only certain prayers show icons)
    private static readonly Dictionary<int, int> HeadIcons = new()
    {
        [16] = 2,  // Protect from Magic
        [17] = 1,  // Protect from Missiles
        [18] = 0,  // Protect from Melee
        [21] = 3,  // Retribution
        [22] = 5,  // Smite
        [23] = 4,  // Redemption
        [24] = 7,  // Summoning (leech/deflect curses variant)
    };

    public ValueTask HandleAsync(IPlayerSession session, PrayerMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        // Map button ID to prayer index (buttons are odd: 5, 7, 9 ... 57 → index 0..26)
        int prayerIndex = (message.ButtonId - 5) / 2;
        if (prayerIndex < 0 || prayerIndex >= PrayerConfig.Length)
            return ValueTask.CompletedTask;

        int prayerSkill = 5; // Prayer skill ID
        if (player.Skills.GetLevel(prayerSkill) <= 0)
            return ValueTask.CompletedTask;

        int requiredLevel = PrayerLevel[prayerIndex];
        if (player.Skills.GetLevelForExperience(player.Skills.GetExperience(prayerSkill)) < requiredLevel)
        {
            // TODO: session.SendMessage($"You need a prayer level of {requiredLevel} to use this.");
            return ValueTask.CompletedTask;
        }

        // Turn off conflicting prayers
        DeactivateConflicts(player, prayerIndex);

        // Toggle the prayer
        player.PrayerActive[prayerIndex] = !player.PrayerActive[prayerIndex];
        // TODO: session.SendConfig(PrayerConfig[prayerIndex], player.PrayerActive[prayerIndex] ? 1 : 0);

        // Update overhead icon for protection / curse prayers
        if (HeadIcons.ContainsKey(prayerIndex))
        {
            if (player.PrayerActive[prayerIndex])
            {
                player.PrayerIcon = HeadIcons[prayerIndex];
            }
            else
            {
                // Fall back to summoning icon if active, else none
                player.PrayerIcon = player.PrayerActive[24] ? 7 : -1;
            }
        }

        // Adjust drain rate
        if (player.PrayerActive[prayerIndex])
            player.PrayerDrainRate += DrainRate[prayerIndex];
        else
            player.PrayerDrainRate -= DrainRate[prayerIndex];

        player.AppearanceUpdateRequired = true;
        player.UpdateRequired = true;

        return ValueTask.CompletedTask;
    }

    private static void DeactivateConflicts(Entities.Player player, int prayerIndex)
    {
        if (prayerIndex < 0 || prayerIndex >= ConflictTable.Length)
            return;

        foreach (int off in ConflictTable[prayerIndex])
        {
            if (off >= 0 && off < player.PrayerActive.Length && player.PrayerActive[off])
            {
                player.PrayerActive[off] = false;
                // TODO: session.SendConfig(PrayerConfig[off], 0);
                player.PrayerDrainRate -= DrainRate[off];
            }
        }
    }

    /// <summary>
    /// Resets all active prayers (e.g. on death or prayer points reaching 0).
    /// </summary>
    public static void ResetPrayers(Entities.Player player)
    {
        for (int i = 0; i < player.PrayerActive.Length; i++)
        {
            if (player.PrayerActive[i])
            {
                player.PrayerActive[i] = false;
                // TODO: session.SendConfig(PrayerConfig[i], 0);
            }
        }

        player.PrayerDrainRate = 0;
        player.PrayerIcon = -1;
        player.AppearanceUpdateRequired = true;
        player.UpdateRequired = true;
    }
}
