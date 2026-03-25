namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Extended item-switch message — handles bank tab dragging and inventory swaps
/// while the bank interface is open.
/// Decoded from the 508 SwitchItems2 packet.
/// </summary>
public readonly record struct SwitchItems2Message(
    int InterfaceId,
    int TabId,
    int FromSlot,
    int ToSlot,
    int FromInterfaceRaw,
    int ToInterfaceRaw);
