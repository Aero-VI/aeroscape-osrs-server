namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Operate/use an item (e.g., right-click "Operate" on a ring of duelling, glory amulet, etc.).
/// In the 508 protocol this is the ItemOperate packet carrying the item id, inventory slot,
/// and the interface hash identifying the container.
/// </summary>
public readonly record struct ItemOperateMessage(int ItemId, int Slot, int InterfaceHash);
