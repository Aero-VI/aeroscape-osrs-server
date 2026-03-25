namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Update the player's weight display (in grams, divided by 1000 for kg display).
/// </summary>
public readonly record struct SendWeightMessage(int WeightGrams);
