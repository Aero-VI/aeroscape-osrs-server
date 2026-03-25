namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Use an inventory item on an NPC.
/// Decoded from the 508 ItemOnNPC packet — carries the item ID, NPC server index,
/// item slot, and the interface the item was used from.
/// </summary>
public readonly record struct ItemOnNPCMessage(int ItemId, int NpcIndex, int ItemSlot, int InterfaceId);
