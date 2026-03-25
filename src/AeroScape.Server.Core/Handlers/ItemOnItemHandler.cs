using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles using one inventory item on another (crafting, combining, etc.).
/// </summary>
public sealed class ItemOnItemHandler : IMessageHandler<ItemOnItemMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, ItemOnItemMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        // Validate both items exist in inventory
        // UsedItemId and UsedWithItemId refer to item definition IDs
        // TODO: Implement crafting/skill recipes (knife on log, chisel on gem, needle on leather, etc.)

        return ValueTask.CompletedTask;
    }
}
