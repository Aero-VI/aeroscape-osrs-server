namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Set a right-click option on other players (e.g., "Follow", "Trade with", "Attack").
/// </summary>
public readonly record struct SetPlayerOptionMessage(int Slot, string OptionText, bool OnTop);
