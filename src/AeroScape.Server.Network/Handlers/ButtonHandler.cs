using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

public sealed class ButtonHandler : IMessageHandler<ButtonClickMessage>
{
    private readonly ProtocolService _protocol;
    private readonly ILogger<ButtonHandler> _logger;

    public ButtonHandler(ProtocolService protocol, ILogger<ButtonHandler> logger)
    {
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ButtonClickMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        _logger.LogTrace("Button click: interface={Interface}, button={Button}",
            message.InterfaceId, message.ButtonId);

        switch (message.InterfaceId)
        {
            // Logout button
            case 2449 when message.ButtonId == 0:
                await PacketSender.SendLogout(ps, _protocol, ct);
                ps.Disconnect("Logout");
                return;

            // Run toggle (settings tab)
            case 904:
                if (message.ButtonId == 0)
                {
                    player.IsRunning = !player.IsRunning;
                    // Config 173 = run mode (0=walk, 1=run)
                    await PacketSender.SendConfig(ps, _protocol, 173, player.IsRunning ? 1 : 0, ct);
                }
                break;

            // Auto-retaliate toggle (attack tab)  
            case 2423:
                player.AutoRetaliate = !player.AutoRetaliate;
                await PacketSender.SendConfig(ps, _protocol, 172, player.AutoRetaliate ? 1 : 0, ct);
                break;

            // Brightness settings
            case 906:
                // Brightness config buttons — acknowledge silently
                break;

            // Music volume
            case 900:
                break;

            // Sound effects volume
            case 898:
                break;

            default:
                _logger.LogTrace("Unhandled button: interface={Interface}, button={Button}",
                    message.InterfaceId, message.ButtonId);
                break;
        }
    }
}
