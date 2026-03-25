namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Use one inventory item on another inventory item.
/// </summary>
public readonly record struct ItemOnItemMessage(int UsedItemId, int UsedWithItemId);
