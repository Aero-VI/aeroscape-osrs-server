using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles ItemOperate packets — the player right-clicked "Operate" on an item.
/// Validates the item exists in the given slot, then delegates to
/// item-specific operate logic (teleport jewellery, special equipment, etc.).
/// </summary>
public sealed class ItemOperateMessageHandler : IMessageHandler<ItemOperateMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, ItemOperateMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        // Validate the item is actually in the given slot
        var item = player.Inventory.Get(message.Slot);
        if (item == null || item.Id != message.ItemId)
            return ValueTask.CompletedTask;

        // TODO: Dispatch to item-specific operate handlers:
        //   - Teleport jewellery (glory, duelling ring, games necklace)
        //   - Equipment special abilities (dragon axe spec, etc.)
        //   - Consumable equipment (ring of recoil check, etc.)
        // TODO: Check item requirements and cooldowns

        return ValueTask.CompletedTask;
    }
}
