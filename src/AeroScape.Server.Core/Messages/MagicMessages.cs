namespace AeroScape.Server.Core.Messages;

// Canonical type is now MagicOnNPCMessage in MagicOnNPCMessage.cs
// Canonical type is now MagicOnPlayerMessage in MagicOnPlayerMessage.cs

/// <summary>
/// Alias — legacy Network-layer code still references this casing.
/// Prefer <see cref="MagicOnNPCMessage"/> for new code.
/// </summary>
public readonly record struct MagicOnNpcMessage(int NpcIndex, int SpellId, int InterfaceId);

/// <summary>
/// Cast a spell on an item.
/// </summary>
public readonly record struct MagicOnItemMessage(int ItemId, int Slot, int SpellId, int InterfaceId);
