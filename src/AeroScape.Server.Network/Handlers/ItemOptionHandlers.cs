using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles the "Operate" action on an equipped item (e.g. glory teleport, ring of duelling).
/// </summary>
public sealed class ItemOperateHandler : IMessageHandler<ItemOperateMessage>
{
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ItemOperateHandler> _logger;

    public ItemOperateHandler(ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<ItemOperateHandler> logger)
    {
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ItemOperateMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        var item = player.Equipment.Get(message.Slot);
        if (item == null || item.Id != message.ItemId) return;

        var def = _itemDefs.Get(message.ItemId);
        var itemName = def?.Name ?? $"Item {message.ItemId}";

        _logger.LogTrace("Player {Name} operated item {Item} (slot {Slot})",
            player.Username, itemName, message.Slot);

        // TODO: Implement glory teleport, ring of duelling, games necklace, etc.
        await PacketSender.SendMessage(ps, _protocol,
            $"Nothing interesting happens. (Operate: {itemName})", ct);
    }
}

/// <summary>
/// Handles the first right-click inventory option on an item (e.g. "Eat", "Drink", "Read").
/// </summary>
public sealed class ItemOption1Handler : IMessageHandler<ItemOption1Message>
{
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ItemOption1Handler> _logger;

    public ItemOption1Handler(ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<ItemOption1Handler> logger)
    {
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ItemOption1Message message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        var item = player.Inventory.Get(message.Slot);
        if (item == null || item.Id != message.ItemId) return;

        var def = _itemDefs.Get(message.ItemId);
        var itemName = def?.Name ?? $"Item {message.ItemId}";

        _logger.LogTrace("Player {Name} used option 1 on {Item} (slot {Slot})",
            player.Username, itemName, message.Slot);

        // TODO: Implement eat, drink, read, bury bones, etc.
        await PacketSender.SendMessage(ps, _protocol,
            $"Nothing interesting happens. ({itemName})", ct);
    }
}

/// <summary>
/// Handles the second right-click inventory option on an item.
/// </summary>
public sealed class ItemOption2Handler : IMessageHandler<ItemOption2Message>
{
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ItemOption2Handler> _logger;

    public ItemOption2Handler(ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<ItemOption2Handler> logger)
    {
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ItemOption2Message message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        var item = player.Inventory.Get(message.Slot);
        if (item == null || item.Id != message.ItemId) return;

        var def = _itemDefs.Get(message.ItemId);
        var itemName = def?.Name ?? $"Item {message.ItemId}";

        _logger.LogTrace("Player {Name} used option 2 on {Item} (slot {Slot})",
            player.Username, itemName, message.Slot);

        // TODO: Implement secondary item actions
        await PacketSender.SendMessage(ps, _protocol,
            $"Nothing interesting happens. ({itemName})", ct);
    }
}

/// <summary>
/// Handles item selection for "Use" — the first click of a use-with interaction.
/// </summary>
public sealed class ItemSelectHandler : IMessageHandler<ItemSelectMessage>
{
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ItemSelectHandler> _logger;

    public ItemSelectHandler(ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<ItemSelectHandler> logger)
    {
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ItemSelectMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        var item = player.Inventory.Get(message.Slot);
        if (item == null || item.Id != message.ItemId) return;

        var def = _itemDefs.Get(message.ItemId);
        var itemName = def?.Name ?? $"Item {message.ItemId}";

        _logger.LogTrace("Player {Name} selected item {Item} for use (slot {Slot})",
            player.Username, itemName, message.Slot);

        // The client handles the "Use X ->" cursor state; the server just needs to 
        // know when the second target is selected (ItemOnItem, ItemOnNpc, etc.)
        // No server response needed for the initial select.
    }
}

/// <summary>
/// Handles extended item switching across interfaces (e.g. bank insert mode).
/// </summary>
public sealed class MoveItemExtendedHandler : IMessageHandler<SwitchItemExtendedMessage>
{
    private readonly ProtocolService _protocol;
    private readonly ILogger<MoveItemExtendedHandler> _logger;

    public MoveItemExtendedHandler(ProtocolService protocol, ILogger<MoveItemExtendedHandler> logger)
    {
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, SwitchItemExtendedMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        _logger.LogTrace("Player {Name} extended switch: from {From} to {To} (interfaces {FI} → {TI})",
            player.Username, message.FromSlot, message.ToSlot, message.FromInterfaceHash, message.ToInterfaceHash);

        // TODO: Bank insert-mode swap, other cross-interface item moves
        // For basic inventory swaps, delegate to inventory logic
        if (message.FromSlot >= 0 && message.FromSlot < 28 &&
            message.ToSlot >= 0 && message.ToSlot < 28)
        {
            player.Inventory.Swap(message.FromSlot, message.ToSlot);
            await PacketSender.SendInventory(ps, _protocol, ct);
        }
    }
}
