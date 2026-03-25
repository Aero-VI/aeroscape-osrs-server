using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles NPCAttack packets — the player has initiated combat with an NPC.
/// Validates the target index, faces the NPC, and sets up the follow target
/// so the player walks toward the NPC and begins fighting.
/// </summary>
public sealed class NPCAttackMessageHandler : IMessageHandler<NPCAttackMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, NPCAttackMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        if (message.NpcIndex < 0)
            return ValueTask.CompletedTask;

        // Face the NPC entity (NPC indices are used directly, no offset)
        player.FaceEntity(message.NpcIndex);

        // Set follow target so the player walks toward the NPC
        player.FollowTargetIndex = message.NpcIndex;

        // TODO: Queue combat task once walk-to completes
        // TODO: Check multi-combat zone rules
        // TODO: Validate NPC is attackable and within range

        return ValueTask.CompletedTask;
    }
}
