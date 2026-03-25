using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

public sealed class EquipItemHandler : IMessageHandler<EquipItemMessage>
{
    private readonly ILogger<EquipItemHandler> _logger;
    public EquipItemHandler(ILogger<EquipItemHandler> logger) => _logger = logger;

    public ValueTask HandleAsync(IPlayerSession session, EquipItemMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        var player = ps.Player;

        // TODO: Look up equipment slot from item definitions
        // TODO: Check level requirements
        // For now, just swap from inventory to equipment slot 0
        var item = player.Inventory.Get(message.Slot);
        if (item != null && item.Id == message.ItemId)
        {
            player.Inventory.Remove(message.Slot);
            // TODO: Map item ID to equipment slot, handle 2H weapons, etc.
            player.AppearanceUpdateRequired = true;
            player.UpdateRequired = true;
            _logger.LogTrace("Player {Name} equipping item {Id} from slot {Slot}",
                player.Username, message.ItemId, message.Slot);
        }
        return ValueTask.CompletedTask;
    }
}

public sealed class DropItemHandler : IMessageHandler<DropItemMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, DropItemMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        var player = ps.Player;
        
        var item = player.Inventory.Get(message.Slot);
        if (item != null && item.Id == message.ItemId)
        {
            player.Inventory.Remove(message.Slot);
            // TODO: Create ground item at player position
            // TODO: Send inventory update packet
        }
        return ValueTask.CompletedTask;
    }
}
