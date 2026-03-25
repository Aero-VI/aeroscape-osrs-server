using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles dropping an item from inventory onto the ground.
/// </summary>
public sealed class DropItemHandler : IMessageHandler<DropItemMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, DropItemMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        var item = player.Inventory.Get(message.Slot);
        if (item == null || item.Id != message.ItemId)
            return ValueTask.CompletedTask;

        // Remove from inventory
        player.Inventory.Remove(message.Slot);

        // Create ground item at player's position
        var groundItem = new GroundItem(item.Id, item.Amount, player.Position, player.Username);
        // Ground item registration is handled by the world service

        return ValueTask.CompletedTask;
    }
}
