using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

public sealed class WalkHandler : IMessageHandler<WalkMessage>
{
    private readonly ILogger<WalkHandler> _logger;

    public WalkHandler(ILogger<WalkHandler> logger) => _logger = logger;

    public ValueTask HandleAsync(IPlayerSession session, WalkMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        
        var player = ps.Player;
        ps.Movement.Reset();

        // Queue destination
        int x = message.DestX;
        int y = message.DestY;
        player.IsRunning = message.Running;

        // Add each step to the movement queue
        ps.Movement.AddStep(new Position(x, y, player.Position.Z));
        
        foreach (var step in message.Steps)
        {
            x += step.DeltaX;
            y += step.DeltaY;
            ps.Movement.AddStep(new Position(x, y, player.Position.Z));
        }

        _logger.LogTrace("Player {Name} walking to ({X},{Y})", player.Username, message.DestX, message.DestY);
        return ValueTask.CompletedTask;
    }
}
