using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles casting a spell on an NPC (e.g. fire blast on a monster).
/// </summary>
public sealed class MagicOnNpcHandler : IMessageHandler<MagicOnNpcMessage>
{
    private readonly GameWorld _world;
    private readonly CombatSystem _combat;
    private readonly ProtocolService _protocol;
    private readonly ILogger<MagicOnNpcHandler> _logger;

    public MagicOnNpcHandler(GameWorld world, CombatSystem combat, ProtocolService protocol, ILogger<MagicOnNpcHandler> logger)
    {
        _world = world;
        _combat = combat;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, MagicOnNpcMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;
        var npc = _world.GetNpc(message.NpcIndex);

        if (npc == null || !npc.IsActive)
        {
            _logger.LogTrace("Magic on NPC with invalid index {Index}", message.NpcIndex);
            return;
        }

        _logger.LogTrace("Player {Name} cast spell {SpellId} on NPC {NpcId}",
            player.Username, message.SpellId, npc.Id);

        player.FaceEntity(message.NpcIndex);

        // TODO: Implement full magic system — rune checks, spell effects, XP
        // For now, treat as a magic-based attack
        _combat.AttackNpc(player, npc);
        player.PlayAnimation(711); // Generic magic cast animation

        await PacketSender.SendMessage(ps, _protocol,
            $"You cast a spell on the {npc.Name}.", ct);
    }
}

/// <summary>
/// Handles casting a spell on another player (PvP magic, teleother, etc.).
/// </summary>
public sealed class MagicOnPlayerHandler : IMessageHandler<MagicOnPlayerMessage>
{
    private readonly GameWorld _world;
    private readonly ProtocolService _protocol;
    private readonly ILogger<MagicOnPlayerHandler> _logger;

    public MagicOnPlayerHandler(GameWorld world, ProtocolService protocol, ILogger<MagicOnPlayerHandler> logger)
    {
        _world = world;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, MagicOnPlayerMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;
        var target = _world.GetPlayer(message.TargetIndex);

        if (target == null || !target.IsActive)
        {
            _logger.LogTrace("Magic on player with invalid index {Index}", message.TargetIndex);
            return;
        }

        _logger.LogTrace("Player {Name} cast spell {SpellId} on player {Target}",
            player.Username, message.SpellId, target.Username);

        player.FaceEntity(message.TargetIndex + 32768);
        player.PlayAnimation(711);

        // TODO: Implement PvP magic, teleother, vengeance other, etc.
        await PacketSender.SendMessage(ps, _protocol,
            $"You cast a spell on {target.Username}.", ct);
    }
}

/// <summary>
/// Handles casting a spell on an inventory item (e.g. High/Low Alchemy, enchantment).
/// </summary>
public sealed class MagicOnItemHandler : IMessageHandler<MagicOnItemMessage>
{
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<MagicOnItemHandler> _logger;

    public MagicOnItemHandler(ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<MagicOnItemHandler> logger)
    {
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, MagicOnItemMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        var item = player.Inventory.Get(message.Slot);
        if (item == null || item.Id != message.ItemId) return;

        var def = _itemDefs.Get(message.ItemId);
        var itemName = def?.Name ?? $"Item {message.ItemId}";

        _logger.LogTrace("Player {Name} cast spell {SpellId} on item {Item} (slot {Slot})",
            player.Username, message.SpellId, itemName, message.Slot);

        player.PlayAnimation(712); // Alchemy animation

        // TODO: Implement High/Low Alchemy, enchant crossbow bolt, superheat, etc.
        await PacketSender.SendMessage(ps, _protocol,
            $"You cast a spell on the {itemName}.", ct);
    }
}
