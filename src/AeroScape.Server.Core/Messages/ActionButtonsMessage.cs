namespace AeroScape.Server.Core.Messages;

/// <summary>
/// An action button press on a game interface (e.g., skill tab, prayer toggle, spell select,
/// emote, combat style switch). In the 508 protocol this is the ActionButtons packet
/// carrying the interface and button identifiers.
/// </summary>
public readonly record struct ActionButtonsMessage(int InterfaceId, int ButtonId);
