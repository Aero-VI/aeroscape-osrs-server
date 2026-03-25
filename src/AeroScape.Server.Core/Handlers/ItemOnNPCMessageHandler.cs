using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles using an inventory item on an NPC.
/// Validates the item exists in inventory and the NPC index is valid,
/// then delegates to the appropriate item-on-NPC interaction logic
/// (e.g., feeding, using bones on an altar NPC, quest item hand-off).
/// </summary>
public sealed class ItemOnNPCMessageHandler : IMessageHandler<ItemOnNPCMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, ItemOnNPCMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        if (message.NpcIndex < 0 || message.ItemSlot < 0 || message.ItemSlot >= player.Inventory.Capacity)
            return ValueTask.CompletedTask;

        // Verify the item is actually in the claimed slot
        var item = player.Inventory.Get(message.ItemSlot);
        if (item == null || item.Id != message.ItemId)
            return ValueTask.CompletedTask;

        // Face the NPC
        player.FaceEntity(message.NpcIndex);

        // TODO: Walk to NPC if not adjacent
        // TODO: Look up item-on-NPC interaction table (quest items, feeding, etc.)
        // TODO: Consume item if applicable

        return ValueTask.CompletedTask;
    }
}
