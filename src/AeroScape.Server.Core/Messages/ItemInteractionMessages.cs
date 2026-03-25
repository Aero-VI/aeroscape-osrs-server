namespace AeroScape.Server.Core.Messages;

// ItemOnItemMessage moved to ItemOnItemMessage.cs
// Canonical type is now ItemOnNPCMessage in ItemOnNPCMessage.cs
// Canonical type is now ItemOnObjectMessage in ItemOnObjectMessage.cs

/// <summary>
/// Alias — legacy Network-layer code still references this casing.
/// Prefer <see cref="ItemOnNPCMessage"/> for new code.
/// </summary>
public readonly record struct ItemOnNpcMessage(int ItemId, int NpcIndex, int ItemSlot, int InterfaceId);

/// <summary>
/// Use an item on another player.
/// </summary>
public readonly record struct ItemOnPlayerMessage(int ItemId, int TargetIndex);
