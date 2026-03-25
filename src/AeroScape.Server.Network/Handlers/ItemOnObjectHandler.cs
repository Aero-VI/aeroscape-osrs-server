using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles using an item on a world object (e.g. ore on furnace, logs on fire).
/// </summary>
public sealed class ItemOnObjectHandler : IMessageHandler<ItemOnObjectMessage>
{
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ItemOnObjectHandler> _logger;

    public ItemOnObjectHandler(ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<ItemOnObjectHandler> logger)
    {
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ItemOnObjectMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        var itemDef = _itemDefs.Get(message.ItemId);
        var itemName = itemDef?.Name ?? $"Item {message.ItemId}";

        _logger.LogTrace("Player {Name} used {Item} on object {ObjId} at ({X},{Y})",
            player.Username, itemName, message.ObjectId, message.ObjectX, message.ObjectY);

        // Face the object
        player.FacePosition(new Position(message.ObjectX, message.ObjectY));

        // TODO: Implement item-on-object interactions (smelting, cooking, etc.)
        await PacketSender.SendMessage(ps, _protocol,
            $"Nothing interesting happens. ({itemName} on object {message.ObjectId})", ct);
    }
}
