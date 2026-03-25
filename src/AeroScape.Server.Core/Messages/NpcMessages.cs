namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Interact with an NPC (talk-to, pickpocket, etc.).
/// </summary>
public readonly record struct NpcInteractMessage(int NpcIndex, int OptionIndex);

/// <summary>
/// Attack an NPC.
/// </summary>
public readonly record struct NpcAttackMessage(int NpcIndex);
