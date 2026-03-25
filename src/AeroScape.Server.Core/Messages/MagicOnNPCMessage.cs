namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Cast a spell on an NPC.
/// Decoded from the 508 MagicOnNPC packet — carries the NPC server index,
/// spell ID, and the spellbook interface ID.
/// </summary>
public readonly record struct MagicOnNPCMessage(int NpcIndex, int SpellId, int InterfaceId);
