namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Drop an item from inventory onto the ground.
/// </summary>
public readonly record struct DropItemMessage(int ItemId, int Slot, int InterfaceId);
