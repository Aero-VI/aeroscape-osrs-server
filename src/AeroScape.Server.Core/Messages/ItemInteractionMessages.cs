namespace AeroScape.Server.Core.Messages;

// ItemOnItemMessage moved to ItemOnItemMessage.cs

/// <summary>
/// Use an item on an NPC.
/// </summary>
public readonly record struct ItemOnNpcMessage(int ItemId, int NpcIndex, int ItemSlot, int InterfaceId);

/// <summary>
/// Use an item on a world object.
/// </summary>
public readonly record struct ItemOnObjectMessage(int ItemId, int ObjectId, int ObjectX, int ObjectY);

/// <summary>
/// Use an item on another player.
/// </summary>
public readonly record struct ItemOnPlayerMessage(int ItemId, int TargetIndex);
