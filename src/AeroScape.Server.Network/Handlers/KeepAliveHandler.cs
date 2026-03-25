using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Session;

namespace AeroScape.Server.Network.Handlers;

public sealed class KeepAliveHandler : IMessageHandler<KeepAliveMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, KeepAliveMessage message, CancellationToken ct)
    {
        // No-op — the client is alive, reset idle timer
        return ValueTask.CompletedTask;
    }
}

public sealed class IdleLogoutHandler : IMessageHandler<IdleLogoutMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, IdleLogoutMessage message, CancellationToken ct)
    {
        session.Disconnect("Idle logout");
        return ValueTask.CompletedTask;
    }
}

public sealed class RegionLoadedHandler : IMessageHandler<RegionLoadedMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, RegionLoadedMessage message, CancellationToken ct)
    {
        // Client finished loading the map region
        return ValueTask.CompletedTask;
    }
}

public sealed class CloseInterfaceHandler : IMessageHandler<CloseInterfaceMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, CloseInterfaceMessage message, CancellationToken ct)
    {
        if (session is PlayerSession ps)
            ps.Player.OpenInterfaceId = -1;
        return ValueTask.CompletedTask;
    }
}
