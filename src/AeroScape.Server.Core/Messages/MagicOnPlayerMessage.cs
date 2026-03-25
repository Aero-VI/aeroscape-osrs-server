namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Cast a spell on another player.
/// Decoded from the 508 MagicOnPlayer packet — carries the target player index,
/// spell ID, and the spellbook interface ID.
/// </summary>
public readonly record struct MagicOnPlayerMessage(int TargetIndex, int SpellId, int InterfaceId);
