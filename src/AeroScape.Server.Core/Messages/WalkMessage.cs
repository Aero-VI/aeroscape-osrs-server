namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Represents a player walk/run request with a destination and intermediate steps.
/// </summary>
public readonly record struct WalkMessage(int DestX, int DestY, bool Running, IReadOnlyList<WalkStep> Steps);

/// <summary>
/// A single intermediate step delta in a walk path.
/// </summary>
public readonly record struct WalkStep(int DeltaX, int DeltaY);
