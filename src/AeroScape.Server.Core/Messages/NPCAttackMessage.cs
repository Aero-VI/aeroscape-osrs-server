namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Attack an NPC. Sent when the player clicks "Attack" on an NPC.
/// In the 508 protocol this is the NPCAttack packet carrying the NPC's server index.
/// </summary>
public readonly record struct NPCAttackMessage(int NpcIndex);
