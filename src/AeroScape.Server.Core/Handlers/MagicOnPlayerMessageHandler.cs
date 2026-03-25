using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles casting a spell on another player.
/// Validates the spell, checks rune requirements, and initiates
/// the magic combat or utility spell sequence against the target player.
/// </summary>
public sealed class MagicOnPlayerMessageHandler : IMessageHandler<MagicOnPlayerMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, MagicOnPlayerMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        if (message.TargetIndex < 0 || message.SpellId < 0)
            return ValueTask.CompletedTask;

        // Prevent self-targeting
        if (message.TargetIndex == player.Index)
            return ValueTask.CompletedTask;

        // Face the target player (player indices offset by 32768 for entity face)
        player.FaceEntity(message.TargetIndex + 32768);

        // TODO: Validate spell ID exists in the player's active spellbook (InterfaceId)
        // TODO: Check magic level requirement
        // TODO: Check and consume runes from inventory
        // TODO: Check if in Wilderness or PvP area (unless utility spell like tele-other)
        // TODO: Check combat range
        // TODO: Play casting animation + graphic
        // TODO: Create projectile and schedule hit task
        // TODO: Apply spell effect (damage, freeze, teleblock, venge, etc.)

        return ValueTask.CompletedTask;
    }
}
