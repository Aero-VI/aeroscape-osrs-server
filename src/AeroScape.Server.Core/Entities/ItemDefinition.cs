namespace AeroScape.Server.Core.Entities;

/// <summary>
/// Static item definition. In a full server these are loaded from cache;
/// here we provide a basic in-memory set covering common items.
/// </summary>
public sealed class ItemDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = "null";
    public string? Examine { get; set; }
    public bool Stackable { get; set; }
    public bool Noted { get; set; }
    public int NoteId { get; set; } = -1;
    public int UnnoteId { get; set; } = -1;
    public bool TwoHanded { get; set; }
    public bool Members { get; set; }
    public int EquipSlot { get; set; } = -1; // -1 = not equippable
    public int HighAlch { get; set; }
    public int LowAlch { get; set; }
    public int Weight { get; set; }
    
    // Equipment bonuses (if equippable)
    public int[] AttackBonuses { get; set; } = new int[5]; // stab, slash, crush, magic, range
    public int[] DefenceBonuses { get; set; } = new int[5];
    public int StrengthBonus { get; set; }
    public int RangeStrengthBonus { get; set; }
    public int MagicDamageBonus { get; set; }
    public int PrayerBonus { get; set; }
    
    // Equipment requirements
    public Dictionary<int, int> Requirements { get; set; } = new(); // skillId -> level

    /// <summary>
    /// Equipment slot constants matching the 508 client.
    /// </summary>
    public static class Slots
    {
        public const int Hat = 0;
        public const int Cape = 1;
        public const int Amulet = 2;
        public const int Weapon = 3;
        public const int Chest = 4;
        public const int Shield = 5;
        public const int Legs = 7;
        public const int Gloves = 9;
        public const int Boots = 10;
        public const int Ring = 12;
        public const int Ammo = 13;
    }
}
