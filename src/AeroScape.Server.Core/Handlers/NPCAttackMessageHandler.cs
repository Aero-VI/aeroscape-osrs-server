using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles NPCAttack packets — the player has initiated combat with an NPC.
/// Validates the target index, checks summoning ownership rules, faces the NPC,
/// and begins the follow + combat sequence.
///
/// Translated from legacy Java: DavidScape.io.packets.NPCAttack
/// </summary>
public sealed class NPCAttackMessageHandler : IMessageHandler<NPCAttackMessage>
{
    private readonly ILogger<NPCAttackMessageHandler> _logger;

    public NPCAttackMessageHandler(ILogger<NPCAttackMessageHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(IPlayerSession session, NPCAttackMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        if (message.NpcIndex < 0)
            return ValueTask.CompletedTask;

        // TODO: Resolve NPC from world NPC list by index
        //   var npc = World.Npcs[message.NpcIndex];
        //   if (npc == null) return;

        // ── Summoned NPC ownership checks (from legacy) ──────────────
        // If the NPC is a summoned familiar:
        //   - If owned by another player and not actively attacking us → reject
        //     ("You cannot attack a player summoned NPC!")
        //   - If owned by this player → pick up the familiar instead
        //     (set FamiliarType = 0, kill NPC, clear FamiliarID)

        // ── Begin combat ─────────────────────────────────────────────
        // 1. Set player.AttackNpcIndex = message.NpcIndex
        // 2. Set npc.FollowPlayerIndex = player index
        // 3. Flag player as attacking NPC (player.AttackingNpc = true)
        // 4. Face the NPC entity

        player.FaceEntity(message.NpcIndex);
        player.FollowTargetIndex = message.NpcIndex;

        // TODO: Queue combat task once walk-to completes
        // TODO: Check multi-combat zone rules
        // TODO: Validate NPC is attackable and within range

        _logger.LogDebug("[{Username}] Attack NPC index={NpcIndex}", player.Username, message.NpcIndex);

        return ValueTask.CompletedTask;
    }
}
