using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Session;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles public chat messages. Sets the chat update flags on the player
/// so that the player update cycle broadcasts the message to nearby players.
/// </summary>
public sealed class ChatHandler : IMessageHandler<PublicChatMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, PublicChatMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        var player = ps.Player;

        player.ChatColor = message.Color;
        player.ChatEffect = message.Effect;
        player.ChatText = message.PackedText;
        player.ChatUpdateRequired = true;
        player.UpdateRequired = true;

        return ValueTask.CompletedTask;
    }
}
