using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
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

        _logger.LogTrace("Button click: interface={Interface}, button={Button}",
            message.InterfaceId, message.ButtonId);

        // Handle logout button
        if (message.InterfaceId == 2449 && message.ButtonId == 0)
        {
            var logoutDef = _protocol.GetOutgoingByName("Logout");
            if (logoutDef != null)
            {
                var pkt = new PacketBuilder();
                await ps.SendPacketAsync(pkt.Build(logoutDef.Opcode, ps.OutgoingCipher), ct);
            }
            ps.Disconnect("Logout");
            return;
        }

        // TODO: Handle other button actions (prayer, spellbook, settings, etc.)
    }
}
