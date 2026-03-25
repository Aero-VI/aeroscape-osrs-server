using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles using an item on an NPC (e.g. bones on altar NPC, food on pet).
/// </summary>
public sealed class ItemOnNpcHandler : IMessageHandler<ItemOnNpcMessage>
{
    private readonly GameWorld _world;
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ItemOnNpcHandler> _logger;

    public ItemOnNpcHandler(GameWorld world, ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<ItemOnNpcHandler> logger)
    {
        _world = world;
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ItemOnNpcMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;
        var npc = _world.GetNpc(message.NpcIndex);

        if (npc == null || !npc.IsActive)
        {
            _logger.LogTrace("Item on NPC with invalid NPC index {Index}", message.NpcIndex);
            return;
        }

        var itemDef = _itemDefs.Get(message.ItemId);
        var itemName = itemDef?.Name ?? $"Item {message.ItemId}";

        _logger.LogTrace("Player {Name} used {Item} on NPC {NpcName} (index {Index})",
            player.Username, itemName, npc.Name, message.NpcIndex);

        // Face the NPC
        player.FaceEntity(message.NpcIndex);

        // TODO: Implement item-on-NPC interactions (e.g. using items in quests)
        await PacketSender.SendMessage(ps, _protocol,
            $"Nothing interesting happens. ({itemName} on {npc.Name})", ct);
    }
}
