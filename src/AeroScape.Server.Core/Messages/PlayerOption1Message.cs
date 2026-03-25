namespace AeroScape.Server.Core.Messages;

/// <summary>
/// First right-click option on another player (e.g. Follow, Challenge, Trade).
/// Sent when the player clicks the first custom option set via PlayerOption outgoing.
/// </summary>
public readonly record struct PlayerOption1Message(int TargetIndex);
