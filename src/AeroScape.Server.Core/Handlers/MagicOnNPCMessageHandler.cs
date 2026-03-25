using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles casting a spell on an NPC.
/// Validates the spell, checks rune requirements, and initiates
/// the magic combat sequence against the target NPC.
/// </summary>
public sealed class MagicOnNPCMessageHandler : IMessageHandler<MagicOnNPCMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, MagicOnNPCMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        if (message.NpcIndex < 0 || message.SpellId < 0)
            return ValueTask.CompletedTask;

        // Face the target NPC
        player.FaceEntity(message.NpcIndex);

        // Set follow target so the player walks into cast range
        player.FollowTargetIndex = message.NpcIndex;

        // TODO: Validate spell ID exists in the player's active spellbook (InterfaceId)
        // TODO: Check magic level requirement
        // TODO: Check and consume runes from inventory
        // TODO: Check combat range (standard spells = 8 tiles, ancients vary)
        // TODO: Play casting animation + graphic
        // TODO: Create projectile and schedule hit task
        // TODO: Apply spell effect (damage, freeze, poison, etc.)

        return ValueTask.CompletedTask;
    }
}
