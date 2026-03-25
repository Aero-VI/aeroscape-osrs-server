using System.Text.Json;
using AeroScape.Server.Core.Entities;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Provides item definitions for the game. Loads from JSON or supplies defaults.
/// </summary>
public sealed class ItemDefinitionService
{
    private readonly Dictionary<int, ItemDefinition> _definitions = new();
    private readonly ILogger<ItemDefinitionService> _logger;

    public ItemDefinitionService(ILogger<ItemDefinitionService> logger)
    {
        _logger = logger;
        LoadDefaults();
    }

    public ItemDefinition? Get(int itemId) =>
        _definitions.GetValueOrDefault(itemId);

    public int GetEquipSlot(int itemId) =>
        _definitions.TryGetValue(itemId, out var def) ? def.EquipSlot : -1;

    public bool IsStackable(int itemId) =>
        _definitions.TryGetValue(itemId, out var def) && def.Stackable;

    public bool IsTwoHanded(int itemId) =>
        _definitions.TryGetValue(itemId, out var def) && def.TwoHanded;

    public async Task LoadFromFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Item definitions file not found: {Path}. Using defaults.", filePath);
            return;
        }

        await using var stream = File.OpenRead(filePath);
        var items = await JsonSerializer.DeserializeAsync<ItemDefinition[]>(stream, cancellationToken: ct);
        if (items == null) return;

        foreach (var item in items)
            _definitions[item.Id] = item;

        _logger.LogInformation("Loaded {Count} item definitions from {Path}", items.Length, filePath);
    }

    /// <summary>
    /// Loads common 508 items with their equipment slots.
    /// This covers the most frequently used items.
    /// </summary>
    private void LoadDefaults()
    {
        // Helmets (slot 0)
        AddEquipDef(1153, "Iron full helm", ItemDefinition.Slots.Hat);
        AddEquipDef(1155, "Iron med helm", ItemDefinition.Slots.Hat);
        AddEquipDef(1163, "Rune full helm", ItemDefinition.Slots.Hat);
        AddEquipDef(3486, "Gilded full helm", ItemDefinition.Slots.Hat);
        AddEquipDef(4716, "Dharok's helm", ItemDefinition.Slots.Hat);
        AddEquipDef(4724, "Guthan's helm", ItemDefinition.Slots.Hat);
        AddEquipDef(4745, "Torag's helm", ItemDefinition.Slots.Hat);
        AddEquipDef(4753, "Verac's helm", ItemDefinition.Slots.Hat);
        AddEquipDef(4708, "Ahrim's hood", ItemDefinition.Slots.Hat);
        AddEquipDef(4732, "Karil's coif", ItemDefinition.Slots.Hat);

        // Capes (slot 1)
        AddEquipDef(1007, "Cape (red)", ItemDefinition.Slots.Cape);
        AddEquipDef(1019, "Cape (black)", ItemDefinition.Slots.Cape);
        AddEquipDef(1021, "Cape (blue)", ItemDefinition.Slots.Cape);
        AddEquipDef(1023, "Cape (yellow)", ItemDefinition.Slots.Cape);
        AddEquipDef(4315, "Legends cape", ItemDefinition.Slots.Cape);
        AddEquipDef(6568, "Obsidian cape", ItemDefinition.Slots.Cape);
        AddEquipDef(6570, "Fire cape", ItemDefinition.Slots.Cape);

        // Amulets (slot 2)
        AddEquipDef(1704, "Amulet of glory", ItemDefinition.Slots.Amulet);
        AddEquipDef(1712, "Amulet of glory(4)", ItemDefinition.Slots.Amulet);
        AddEquipDef(1725, "Amulet of strength", ItemDefinition.Slots.Amulet);
        AddEquipDef(1731, "Amulet of power", ItemDefinition.Slots.Amulet);
        AddEquipDef(6585, "Amulet of fury", ItemDefinition.Slots.Amulet);

        // Weapons (slot 3)
        AddEquipDef(4151, "Abyssal whip", ItemDefinition.Slots.Weapon);
        AddEquipDef(1333, "Rune scimitar", ItemDefinition.Slots.Weapon);
        AddEquipDef(4587, "Dragon scimitar", ItemDefinition.Slots.Weapon);
        AddEquipDef(1215, "Dragon dagger", ItemDefinition.Slots.Weapon);
        AddEquipDef(5698, "Dragon dagger(p++)", ItemDefinition.Slots.Weapon);
        AddEquipDef(4718, "Dharok's greataxe", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(4726, "Guthan's warspear", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(4747, "Torag's hammers", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(4755, "Verac's flail", ItemDefinition.Slots.Weapon);
        AddEquipDef(4710, "Ahrim's staff", ItemDefinition.Slots.Weapon);
        AddEquipDef(4734, "Karil's crossbow", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(11694, "Armadyl godsword", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(11696, "Bandos godsword", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(11698, "Saradomin godsword", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(11700, "Zamorak godsword", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(11730, "Saradomin sword", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(861, "Magic shortbow", ItemDefinition.Slots.Weapon, twoHanded: true);
        AddEquipDef(853, "Maple shortbow", ItemDefinition.Slots.Weapon, twoHanded: true);

        // Body (slot 4)
        AddEquipDef(1127, "Rune platebody", ItemDefinition.Slots.Chest);
        AddEquipDef(1115, "Iron platebody", ItemDefinition.Slots.Chest);
        AddEquipDef(2503, "Black d'hide body", ItemDefinition.Slots.Chest);
        AddEquipDef(4720, "Dharok's platebody", ItemDefinition.Slots.Chest);
        AddEquipDef(4728, "Guthan's platebody", ItemDefinition.Slots.Chest);
        AddEquipDef(4749, "Torag's platebody", ItemDefinition.Slots.Chest);
        AddEquipDef(4757, "Verac's brassard", ItemDefinition.Slots.Chest);
        AddEquipDef(4712, "Ahrim's robetop", ItemDefinition.Slots.Chest);
        AddEquipDef(4736, "Karil's leathertop", ItemDefinition.Slots.Chest);
        AddEquipDef(11724, "Bandos chestplate", ItemDefinition.Slots.Chest);

        // Shield (slot 5)
        AddEquipDef(1201, "Rune kiteshield", ItemDefinition.Slots.Shield);
        AddEquipDef(1171, "Wooden shield", ItemDefinition.Slots.Shield);
        AddEquipDef(3122, "Granite shield", ItemDefinition.Slots.Shield);
        AddEquipDef(6524, "Toktz-ket-xil", ItemDefinition.Slots.Shield); // Obsidian shield
        AddEquipDef(11726, "Armadyl buckler", ItemDefinition.Slots.Shield);

        // Legs (slot 7)
        AddEquipDef(1079, "Rune platelegs", ItemDefinition.Slots.Legs);
        AddEquipDef(1093, "Rune plateskirt", ItemDefinition.Slots.Legs);
        AddEquipDef(2497, "Black d'hide chaps", ItemDefinition.Slots.Legs);
        AddEquipDef(4722, "Dharok's platelegs", ItemDefinition.Slots.Legs);
        AddEquipDef(4730, "Guthan's chainskirt", ItemDefinition.Slots.Legs);
        AddEquipDef(4751, "Torag's platelegs", ItemDefinition.Slots.Legs);
        AddEquipDef(4759, "Verac's plateskirt", ItemDefinition.Slots.Legs);
        AddEquipDef(4714, "Ahrim's robeskirt", ItemDefinition.Slots.Legs);
        AddEquipDef(4738, "Karil's leatherskirt", ItemDefinition.Slots.Legs);
        AddEquipDef(11726, "Bandos tassets", ItemDefinition.Slots.Legs);

        // Gloves (slot 9)
        AddEquipDef(7462, "Barrows gloves", ItemDefinition.Slots.Gloves);
        AddEquipDef(7461, "Dragon gloves", ItemDefinition.Slots.Gloves);
        AddEquipDef(1059, "Leather gloves", ItemDefinition.Slots.Gloves);

        // Boots (slot 10)
        AddEquipDef(11732, "Dragon boots", ItemDefinition.Slots.Boots);
        AddEquipDef(3105, "Climbing boots", ItemDefinition.Slots.Boots);
        AddEquipDef(1061, "Leather boots", ItemDefinition.Slots.Boots);
        AddEquipDef(2577, "Ranger boots", ItemDefinition.Slots.Boots);

        // Ring (slot 12)
        AddEquipDef(2550, "Ring of recoil", ItemDefinition.Slots.Ring);
        AddEquipDef(6737, "Berserker ring", ItemDefinition.Slots.Ring);
        AddEquipDef(6733, "Archer's ring", ItemDefinition.Slots.Ring);
        AddEquipDef(6731, "Seers ring", ItemDefinition.Slots.Ring);

        // Ammo (slot 13)
        AddEquipDef(892, "Rune arrow", ItemDefinition.Slots.Ammo, stackable: true);
        AddEquipDef(890, "Adamant arrow", ItemDefinition.Slots.Ammo, stackable: true);
        AddEquipDef(888, "Mithril arrow", ItemDefinition.Slots.Ammo, stackable: true);
        AddEquipDef(886, "Steel arrow", ItemDefinition.Slots.Ammo, stackable: true);
        AddEquipDef(884, "Iron arrow", ItemDefinition.Slots.Ammo, stackable: true);
        AddEquipDef(882, "Bronze arrow", ItemDefinition.Slots.Ammo, stackable: true);
        AddEquipDef(9244, "Dragon bolts (e)", ItemDefinition.Slots.Ammo, stackable: true);

        // Stackable items
        AddStackable(995, "Coins");
        AddStackable(556, "Air rune");
        AddStackable(555, "Water rune");
        AddStackable(557, "Earth rune");
        AddStackable(554, "Fire rune");
        AddStackable(558, "Mind rune");
        AddStackable(559, "Body rune");
        AddStackable(564, "Cosmic rune");
        AddStackable(562, "Chaos rune");
        AddStackable(561, "Nature rune");
        AddStackable(563, "Law rune");
        AddStackable(560, "Death rune");
        AddStackable(565, "Blood rune");
        AddStackable(566, "Soul rune");

        _logger.LogInformation("Loaded {Count} default item definitions", _definitions.Count);
    }

    private void AddEquipDef(int id, string name, int slot, bool twoHanded = false, bool stackable = false)
    {
        _definitions[id] = new ItemDefinition
        {
            Id = id,
            Name = name,
            EquipSlot = slot,
            TwoHanded = twoHanded,
            Stackable = stackable
        };
    }

    private void AddStackable(int id, string name)
    {
        _definitions[id] = new ItemDefinition
        {
            Id = id,
            Name = name,
            Stackable = true
        };
    }
}
