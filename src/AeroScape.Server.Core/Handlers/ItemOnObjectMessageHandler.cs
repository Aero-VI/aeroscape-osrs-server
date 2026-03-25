using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles using an inventory item on a world object.
/// Validates the item and object, then delegates to the appropriate
/// skill or interaction logic (e.g., tinderbox on logs, ore on furnace,
/// knife on tree, key on door).
/// </summary>
public sealed class ItemOnObjectMessageHandler : IMessageHandler<ItemOnObjectMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, ItemOnObjectMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        if (message.ObjectId < 0)
            return ValueTask.CompletedTask;

        // Face the object location
        player.FacePosition(new Entities.Position(message.ObjectX, message.ObjectY, player.Position.Z));

        // TODO: Walk to object if not adjacent
        // TODO: Validate object exists at the given coordinates in the region
        // TODO: Look up item-on-object interaction table
        //       - Tinderbox on logs → Firemaking
        //       - Ore on furnace → Smelting
        //       - Bar on anvil → Smithing
        //       - Knife on tree → Woodcutting special
        //       - Key on door → Quest progression
        // TODO: Consume item if applicable, play animation

        return ValueTask.CompletedTask;
    }
}
