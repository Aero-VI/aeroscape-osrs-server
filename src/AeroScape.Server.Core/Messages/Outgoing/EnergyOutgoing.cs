namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Update the player's run energy orb value (0-100).
/// </summary>
public readonly record struct SendEnergyMessage(int Energy);
