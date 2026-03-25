using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Session;

namespace AeroScape.Server.Network.Handlers;

public sealed class AppearanceHandler : IMessageHandler<AppearanceUpdateMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, AppearanceUpdateMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        
        var player = ps.Player;
        player.Appearance.Gender = message.Gender;
        player.Appearance.Look = message.Look;
        player.Appearance.Colors = message.Colors;
        player.AppearanceUpdateRequired = true;
        player.UpdateRequired = true;
        
        return ValueTask.CompletedTask;
    }
}
