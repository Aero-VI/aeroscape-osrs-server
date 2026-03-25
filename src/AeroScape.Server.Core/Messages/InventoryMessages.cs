namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Move an item within a container.
/// </summary>
public readonly record struct MoveItemMessage(int InterfaceId, int FromSlot, int ToSlot);

// DropItemMessage moved to DropItemMessage.cs

// ItemOperateMessage moved to ItemOperateMessage.cs

/// <summary>
/// First right-click option on an item.
/// </summary>
public readonly record struct ItemOption1Message(int ItemId, int Slot, int InterfaceId);

/// <summary>
/// Second right-click option on an item.
/// </summary>
public readonly record struct ItemOption2Message(int ItemId, int Slot, int InterfaceId);

/// <summary>
/// Select an item (e.g., for use-with).
/// </summary>
public readonly record struct ItemSelectMessage(int ItemId, int Slot, int InterfaceId);

/// <summary>
/// Swap/switch item positions within a container.
/// </summary>
public readonly record struct SwitchItemMessage(int FromSlot, int ToSlot, int InterfaceId);

/// <summary>
/// Switch items across different interfaces (bank insert, etc.).
/// </summary>
public readonly record struct SwitchItemExtendedMessage(int FromSlot, int ToSlot, int FromInterfaceHash, int ToInterfaceHash);
