namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Build/place an object in a player-owned house (Construction skill).
/// </summary>
public readonly record struct ObjectBuildMessage(int ObjectId, int X, int Y);
