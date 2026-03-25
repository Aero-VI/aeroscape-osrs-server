using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles clan chat packets — joining, leaving, messaging, and kicking.
///
/// Translated from legacy Java: DavidScape.io.packets.ClanChat
/// (The legacy class was an empty stub; this handler provides the structure
/// for a full clan chat implementation.)
///
/// NOTE: Clan chat state (channels, ranks, members) should be managed by
/// a dedicated ClanChatService registered in DI, not on the handler.
/// </summary>
public sealed class ClanChatMessageHandler : IMessageHandler<ClanChatMessage>
{
    private readonly ILogger<ClanChatMessageHandler> _logger;

    public ClanChatMessageHandler(ILogger<ClanChatMessageHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(IPlayerSession session, ClanChatMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        switch (message.Action)
        {
            case ClanChatAction.Join:
                // TODO: Delegate to ClanChatService.JoinChannel(player, channelName)
                //   - Validate channel exists or create if owned by player
                //   - Check rank requirements
                //   - Broadcast join notification to members
                _logger.LogDebug("[{Username}] Clan chat join requested", player.Username);
                break;

            case ClanChatAction.Leave:
                // TODO: Delegate to ClanChatService.LeaveChannel(player)
                //   - Remove from active channel member list
                //   - Broadcast leave notification
                _logger.LogDebug("[{Username}] Clan chat leave requested", player.Username);
                break;

            case ClanChatAction.SendMessage:
                // TODO: Delegate to ClanChatService.SendMessage(player, text)
                //   - Validate player is in a channel
                //   - Check mute/rank restrictions
                //   - Broadcast message to all channel members
                _logger.LogDebug("[{Username}] Clan chat message: {Text}", player.Username, message.Text);
                break;

            case ClanChatAction.Kick:
                // TODO: Delegate to ClanChatService.KickMember(player, targetName)
                //   - Validate kicker has sufficient rank
                //   - Remove target from channel
                //   - Send notification to both parties
                _logger.LogDebug("[{Username}] Clan chat kick requested", player.Username);
                break;

            default:
                _logger.LogWarning("[{Username}] Unknown ClanChat action: {Action}",
                    player.Username, message.Action);
                break;
        }

        return ValueTask.CompletedTask;
    }
}
