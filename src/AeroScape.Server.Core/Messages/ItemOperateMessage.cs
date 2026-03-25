namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Operate an equipped item (e.g., right-click "Operate" on a ring of duelling,
/// glory amulet, dragon fire shield, etc.).
/// In the 508 protocol this is the ItemOperate packet carrying a 4-byte interface hash,
/// the item id (unsigned word A), and the equipment slot (unsigned word big-endian A).
///
/// Translated from legacy Java: DavidScape.io.packets.ItemOperate
/// </summary>
public readonly record struct ItemOperateMessage(int ItemId, int Slot, int InterfaceHash);
