namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Pick up or interact with a ground item.
/// </summary>
public readonly record struct GroundItemInteractMessage(int ItemId, int X, int Y);
