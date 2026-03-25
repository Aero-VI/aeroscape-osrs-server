namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Instructs the client to load a new map region centered on the given chunk coordinates.
/// </summary>
public readonly record struct SendMapRegionMessage(int ChunkX, int ChunkY);
