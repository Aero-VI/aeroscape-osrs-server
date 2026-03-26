using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Skills;

/// <summary>
/// Construction skill — ported from legacy Java Construction.java.
/// Room data, building spots, furniture placement, and POH teleportation.
/// Skill index: 22 (Construction).
/// </summary>
public sealed class ConstructionService
{
    private const int SkillId = 22;

    /// <summary>Room info: X, Y, Price, Level required (from legacy roomInfo[][]).</summary>
    public static readonly (int X, int Y, int Price, int Level)[] RoomInfo =
    [
        (1864, 5056, 0, 0),       // Unbuilt land
        (1856, 5112, 1000, 1),    // Parlour
        (1856, 5064, 1000, 1),    // Garden
        (1872, 5112, 5000, 5),    // Kitchen
        (1890, 5112, 5000, 10),   // Dining room
        (1856, 5096, 10000, 15),  // Workshop
        (1904, 5112, 10000, 20),  // Bedroom
        (1880, 5104, 15000, 25),  // Skill hall
        (1896, 5088, 25000, 30),  // Games room
        (1880, 5088, 25000, 32),  // Combat room
        (1912, 5104, 25000, 35),  // Quest hall
        (1888, 5096, 50000, 40),  // Study
        (1904, 5064, 50000, 42),  // Costume room
        (1872, 5096, 50000, 45),  // Chapel
        (1864, 5088, 100000, 50), // Portal chamber
        (1872, 5064, 75000, 55),  // Formal garden
        (1904, 5096, 150000, 60), // Throne room
        (1904, 5080, 150000, 65), // Oubliette
        (1888, 5080, 7500, 70),   // Dungeon - Corridor
        (1856, 5080, 7500, 70),   // Dungeon - Junction
        (1872, 5080, 7500, 70),   // Dungeon - Stairs
        (1912, 5088, 250000, 75), // Treasure room
    ];

    // Garden room coords X per build spot (from legacy roomCoordsX)
    public static readonly int[][] GardenCoordsX =
    [
        [-1,-1,-1,-1,-1,-1,-1,-1,-1,-1], // Empty
        [-1,-1,-1,-1,-1,-1,-1,-1,-1,-1], // Parlour
        [3, 3, 4, 6, 0, 6, 1,-1,-1,-1], // Garden
        [-1,-1,-1,-1,-1,-1,-1,-1,-1,-1], // Kitchen
        [-1,-1,-1,-1,-1,-1,-1,-1,-1,-1], // Dining room
    ];

    public static readonly int[][] GardenCoordsY =
    [
        [-1,-1,-1,-1,-1,-1,-1,-1,-1,-1],
        [-1,-1,-1,-1,-1,-1,-1,-1,-1,-1],
        [3, 1, 5, 0, 0, 6, 5,-1,-1,-1],
        [-1,-1,-1,-1,-1,-1,-1,-1,-1,-1],
        [-1,-1,-1,-1,-1,-1,-1,-1,-1,-1],
    ];

    // Watering can IDs (5333-5340)
    public static bool HasWateringCan(Player player)
    {
        for (int can = 5333; can <= 5340; can++)
        {
            if (player.Inventory.Contains(can))
                return true;
        }
        return false;
    }

    public static void DecreaseWateringCan(Player player)
    {
        for (int can = 5333; can <= 5340; can++)
        {
            if (player.Inventory.Contains(can))
            {
                player.Inventory.RemoveById(can, 1);
                player.Inventory.Add(new Item(can - 1, 1));
                return;
            }
        }
    }

    /// <summary>Check if player can add a room of the given type.</summary>
    public static bool CanAddRoom(Player player, int roomId)
    {
        if (roomId < 0 || roomId >= RoomInfo.Length) return false;
        var info = RoomInfo[roomId];
        return player.Skills.GetLevel(SkillId) >= info.Level
            && player.Inventory.CountOf(995) >= info.Price;
    }

    /// <summary>Add construction XP and optionally farming XP for plant items.</summary>
    public static void AddFurnitureXp(Player player, int conXp, bool isFarming = false)
    {
        player.Skills.AddExperience(SkillId, conXp);
        if (isFarming)
            player.Skills.AddExperience(19, conXp); // Farming XP
    }
}
