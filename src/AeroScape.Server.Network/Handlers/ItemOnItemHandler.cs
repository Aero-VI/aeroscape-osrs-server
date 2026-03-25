using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles using one inventory item on another.
/// Translated from legacy ItemOnItem.java.
/// </summary>
public sealed class ItemOnItemHandler : IMessageHandler<ItemOnItemMessage>
{
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ItemOnItemHandler> _logger;

    public ItemOnItemHandler(ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<ItemOnItemHandler> logger)
    {
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ItemOnItemMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        var usedItem = _itemDefs.Get(message.UsedItemId);
        var withItem = _itemDefs.Get(message.UsedWithItemId);

        _logger.LogTrace("Player {Name} used item {UsedId} on item {WithId}",
            player.Username, message.UsedItemId, message.UsedWithItemId);

        // TODO: Implement crafting/skill recipes (e.g. knife on log, chisel on gem, etc.)
        // For now, send a placeholder message
        var usedName = usedItem?.Name ?? $"Item {message.UsedItemId}";
        var withName = withItem?.Name ?? $"Item {message.UsedWithItemId}";

        await PacketSender.SendMessage(ps, _protocol,
            $"Nothing interesting happens. ({usedName} on {withName})", ct);
    }
}
