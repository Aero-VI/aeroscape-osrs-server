namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// A single item slot in a container update.
/// </summary>
public readonly record struct ItemSlot(int Slot, int ItemId, int Amount);

/// <summary>
/// Send a full container (inventory, bank, equipment, shop, etc.) to the client.
/// </summary>
public readonly record struct SetItemsMessage(int InterfaceId, IReadOnlyList<ItemSlot> Items);
