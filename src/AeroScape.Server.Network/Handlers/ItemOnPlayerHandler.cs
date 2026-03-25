using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles using an item on another player (e.g. Christmas cracker, spell casting via item).
/// </summary>
public sealed class ItemOnPlayerHandler : IMessageHandler<ItemOnPlayerMessage>
{
    private readonly GameWorld _world;
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ItemOnPlayerHandler> _logger;

    public ItemOnPlayerHandler(GameWorld world, ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<ItemOnPlayerHandler> logger)
    {
        _world = world;
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ItemOnPlayerMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;
        var target = _world.GetPlayer(message.TargetIndex);

        if (target == null || !target.IsActive)
        {
            _logger.LogTrace("Item on player with invalid target index {Index}", message.TargetIndex);
            return;
        }

        var itemDef = _itemDefs.Get(message.ItemId);
        var itemName = itemDef?.Name ?? $"Item {message.ItemId}";

        _logger.LogTrace("Player {Name} used {Item} on player {Target}",
            player.Username, itemName, target.Username);

        // Face the target player
        player.FaceEntity(message.TargetIndex + 32768); // Player indices offset by 32768

        // TODO: Implement item-on-player interactions (crackers, quest items, etc.)
        await PacketSender.SendMessage(ps, _protocol,
            $"Nothing interesting happens. ({itemName} on {target.Username})", ct);
    }
}
