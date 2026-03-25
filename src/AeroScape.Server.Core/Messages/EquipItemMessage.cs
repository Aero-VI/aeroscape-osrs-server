namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Equip an item from inventory.
/// </summary>
public readonly record struct EquipItemMessage(int ItemId, int Slot, int InterfaceId);

/// <summary>
/// Unequip an item back to inventory.
/// </summary>
public readonly record struct UnequipItemMessage(int Slot, int InterfaceId);
