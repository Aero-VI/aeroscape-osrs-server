namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Switch (swap) two items on an interface — typically inventory.
/// Decoded from the 508 SwitchItems packet.
/// </summary>
public readonly record struct SwitchItemsMessage(int InterfaceId, int FromSlot, int ToSlot);
