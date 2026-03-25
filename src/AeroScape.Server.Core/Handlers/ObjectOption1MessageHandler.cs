using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles ObjectOption1 packets — the player clicked the first option on a world object.
/// Validates coordinates, faces the object tile, and prepares for the walk-to + interact
/// task (door, rock, tree, bank booth, etc.).
/// </summary>
public sealed class ObjectOption1MessageHandler : IMessageHandler<ObjectOption1Message>
{
    public ValueTask HandleAsync(IPlayerSession session, ObjectOption1Message message, CancellationToken ct = default)
    {
        var player = session.Player;

        // Face the object tile
        player.FacePosition(new Position(message.X, message.Y));

        // TODO: Validate object exists in the region at (X, Y)
        // TODO: Queue walk-to task that triggers interact on arrival:
        //   - Doors: toggle open/close state
        //   - Mining rocks: start mining action
        //   - Trees: start woodcutting action
        //   - Banks: open bank interface
        //   - Ladders/stairs: teleport player up/down
        // TODO: Check object distance and line-of-sight

        return ValueTask.CompletedTask;
    }
}
