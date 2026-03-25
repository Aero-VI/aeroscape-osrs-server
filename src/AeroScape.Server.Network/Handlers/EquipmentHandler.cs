using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

public sealed class EquipItemHandler : IMessageHandler<EquipItemMessage>
{
    private readonly ItemDefinitionService _itemDefs;
    private readonly ILogger<EquipItemHandler> _logger;

    public EquipItemHandler(ItemDefinitionService itemDefs, ILogger<EquipItemHandler> logger)
    {
        _itemDefs = itemDefs;
        _logger = logger;
    }

    public ValueTask HandleAsync(IPlayerSession session, EquipItemMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        var player = ps.Player;

        var item = player.Inventory.Get(message.Slot);
        if (item == null || item.Id != message.ItemId) return ValueTask.CompletedTask;

        int equipSlot = _itemDefs.GetEquipSlot(item.Id);
        if (equipSlot == -1)
        {
            _logger.LogTrace("Item {Id} is not equippable", item.Id);
            return ValueTask.CompletedTask;
        }

        // Remove from inventory
        player.Inventory.Remove(message.Slot);

        // If two-handed weapon, also unequip shield
        if (_itemDefs.IsTwoHanded(item.Id))
        {
            var shield = player.Equipment.Get(ItemDefinition.Slots.Shield);
            if (shield != null)
            {
                player.Equipment.Remove(ItemDefinition.Slots.Shield);
                if (!player.Inventory.Add(shield))
                {
                    player.Inventory.Set(message.Slot, shield);
                }
            }
        }

        // If equipping shield and wearing 2H weapon, unequip weapon
        if (equipSlot == ItemDefinition.Slots.Shield)
        {
            var weapon = player.Equipment.Get(ItemDefinition.Slots.Weapon);
            if (weapon != null && _itemDefs.IsTwoHanded(weapon.Id))
            {
                player.Equipment.Remove(ItemDefinition.Slots.Weapon);
                if (!player.Inventory.Add(weapon))
                {
                    player.Inventory.Set(message.Slot, weapon);
                }
            }
        }

        // Swap with currently equipped item
        var currentlyEquipped = player.Equipment.Get(equipSlot);
        if (currentlyEquipped != null)
        {
            player.Equipment.Remove(equipSlot);
            player.Inventory.Set(message.Slot, currentlyEquipped);
        }

        player.Equipment.Set(equipSlot, item);
        player.AppearanceUpdateRequired = true;
        player.UpdateRequired = true;

        _logger.LogTrace("Player {Name} equipped {Id} in slot {Slot}",
            player.Username, item.Id, equipSlot);
        return ValueTask.CompletedTask;
    }
}

public sealed class UnequipItemHandler : IMessageHandler<UnequipItemMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, UnequipItemMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        var player = ps.Player;

        var item = player.Equipment.Get(message.Slot);
        if (item == null) return ValueTask.CompletedTask;

        int freeSlot = player.Inventory.FreeSlot();
        if (freeSlot == -1) return ValueTask.CompletedTask; // Inventory full

        player.Equipment.Remove(message.Slot);
        player.Inventory.Set(freeSlot, item);
        player.AppearanceUpdateRequired = true;
        player.UpdateRequired = true;

        return ValueTask.CompletedTask;
    }
}

public sealed class DropItemHandler : IMessageHandler<DropItemMessage>
{
    private readonly GameWorld _world;

    public DropItemHandler(GameWorld world) => _world = world;

    public ValueTask HandleAsync(IPlayerSession session, DropItemMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        var player = ps.Player;
        
        var item = player.Inventory.Get(message.Slot);
        if (item == null || item.Id != message.ItemId) return ValueTask.CompletedTask;

        player.Inventory.Remove(message.Slot);

        // Create ground item
        var groundItem = new GroundItem(item.Id, item.Amount, player.Position, player.Username);
        _world.AddGroundItem(groundItem);

        return ValueTask.CompletedTask;
    }
}

public sealed class MoveItemHandler : IMessageHandler<MoveItemMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, MoveItemMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        var player = ps.Player;

        // Inventory swap
        if (message.FromSlot >= 0 && message.FromSlot < 28 &&
            message.ToSlot >= 0 && message.ToSlot < 28)
        {
            player.Inventory.Swap(message.FromSlot, message.ToSlot);
        }

        return ValueTask.CompletedTask;
    }
}
