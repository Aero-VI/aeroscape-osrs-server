namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Interact with another player (follow, trade, attack, etc.).
/// </summary>
public readonly record struct PlayerInteractMessage(int TargetIndex, int OptionIndex);
