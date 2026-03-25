namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Interact with a world object (open door, mine rock, etc.).
/// </summary>
public readonly record struct ObjectInteractMessage(int ObjectId, int X, int Y, int OptionIndex);
