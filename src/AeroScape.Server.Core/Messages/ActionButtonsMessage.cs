namespace AeroScape.Server.Core.Messages;

/// <summary>
/// An action button press on a game interface (e.g., skill tab, prayer toggle, spell select,
/// emote, combat style switch). In the 508 protocol this is the ActionButtons packet
/// carrying the interface and button identifiers.
///
/// ButtonId2 is an optional secondary identifier sent only for certain packet opcodes
/// (233, 21, 169, 232) — used for bank quantities, shop options, etc.
/// A value of 0 means "not provided" (the legacy client sent 65535 which was normalised to 0).
/// </summary>
public readonly record struct ActionButtonsMessage(
    int InterfaceId,
    int ButtonId,
    int ButtonId2 = 0,
    int PacketId = 0);
