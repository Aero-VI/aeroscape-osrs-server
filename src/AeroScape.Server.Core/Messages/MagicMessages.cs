namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Cast a spell on an NPC.
/// </summary>
public readonly record struct MagicOnNpcMessage(int NpcIndex, int SpellId, int InterfaceId);

/// <summary>
/// Cast a spell on another player.
/// </summary>
public readonly record struct MagicOnPlayerMessage(int TargetIndex, int SpellId, int InterfaceId);

/// <summary>
/// Cast a spell on an item.
/// </summary>
public readonly record struct MagicOnItemMessage(int ItemId, int Slot, int SpellId, int InterfaceId);
