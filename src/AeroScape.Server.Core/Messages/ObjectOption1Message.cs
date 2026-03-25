namespace AeroScape.Server.Core.Messages;

/// <summary>
/// First interaction option on a world object (e.g., "Open" door, "Mine" rock, "Chop" tree).
/// In the 508 protocol this is the ObjectOption1 packet carrying the object id and coordinates.
/// </summary>
public readonly record struct ObjectOption1Message(int ObjectId, int X, int Y);
