using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles the first right-click option on another player.
/// This is the custom option set by the server (e.g. "Follow", "Challenge", "Trade").
/// </summary>
public sealed class PlayerOption1MessageHandler : IMessageHandler<PlayerOption1Message>
{
    public ValueTask HandleAsync(IPlayerSession session, PlayerOption1Message message, CancellationToken ct = default)
    {
        var player = session.Player;

        // Validate target exists
        if (message.TargetIndex < 0)
            return ValueTask.CompletedTask;

        // Face the target player (player entity indices offset by 32768)
        player.FaceEntity(message.TargetIndex + 32768);

        // TODO: Implement follow, trade request, duel challenge based on context
        // Default behavior: follow the target
        player.FollowTargetIndex = message.TargetIndex;

        return ValueTask.CompletedTask;
    }
}
