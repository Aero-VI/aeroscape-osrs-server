namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Use an inventory item on a world object.
/// Decoded from the 508 ItemOnObject packet — carries the item ID, object ID,
/// and the object's world coordinates.
/// </summary>
public readonly record struct ItemOnObjectMessage(int ItemId, int ObjectId, int ObjectX, int ObjectY);
