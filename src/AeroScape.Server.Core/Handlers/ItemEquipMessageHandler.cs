using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles equipping an item from inventory to the equipment slot.
/// </summary>
public sealed class ItemEquipMessageHandler : IMessageHandler<EquipItemMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, EquipItemMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        var item = player.Inventory.Get(message.Slot);
        if (item == null || item.Id != message.ItemId)
            return ValueTask.CompletedTask;

        // TODO: Validate equip requirements (level, quest, etc.)
        // TODO: Handle two-handed weapon / shield swap logic
        // Full implementation in AeroScape.Server.Network.Handlers.EquipItemHandler

        return ValueTask.CompletedTask;
    }
}
