using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Ground item management — ported from legacy Java GroundItem.java / Items.java.
/// Handles item drops, pickup, despawn timers, and visibility transitions
/// (private → global → despawn). Wraps GameWorld's ground item list with
/// legacy-accurate timing (240 ticks total, global after 60 ticks remaining).
/// </summary>
public sealed class GroundItemService
{
    private readonly GameWorld _world;

    public GroundItemService(GameWorld world)
    {
        _world = world;
    }

    /// <summary>Create a ground item dropped by a player (from legacy createGroundItem).</summary>
    public void CreateGroundItem(int itemId, int amount, Position position, string droppedBy)
    {
        // Check if same item already exists at position — stack it (from legacy)
        var existingItems = _world.GetGroundItems(position);
        var existing = existingItems.FirstOrDefault(g => g.ItemId == itemId);

        if (existing != null)
        {
            existing.Amount += amount;
            return;
        }

        var item = new GroundItem(itemId, amount, position, droppedBy)
        {
            TicksRemaining = 240,     // From legacy: default 240 ticks
            PublicAfterTicks = 180     // Becomes public at 60 ticks remaining (240-180=60)
        };
        _world.AddGroundItem(item);
    }

    /// <summary>Pick up a ground item. Returns true if found and removed.</summary>
    public bool PickUp(Player player, int itemId, Position position)
    {
        var items = _world.GetGroundItems(position);
        var item = items.FirstOrDefault(g => g.ItemId == itemId
            && (g.IsPublic || g.Owner == player.Username));

        if (item == null) return false;

        if (player.Inventory.Add(new Item(item.ItemId, item.Amount)))
        {
            _world.RemoveGroundItem(itemId, position, item.Owner);
            return true;
        }
        return false; // No inventory space
    }

    /// <summary>Check if an item exists at a position (from legacy itemExists).</summary>
    public GroundItem? FindItem(int itemId, Position position)
    {
        return _world.GetGroundItems(position)
            .FirstOrDefault(g => g.ItemId == itemId);
    }
}
