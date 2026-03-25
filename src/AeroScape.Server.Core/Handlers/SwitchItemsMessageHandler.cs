using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles swapping two items within an interface container (inventory).
/// Translated from legacy Java: DavidScape.io.packets.SwitchItems
/// </summary>
public sealed class SwitchItemsMessageHandler : IMessageHandler<SwitchItemsMessage>
{
    private readonly ILogger<SwitchItemsMessageHandler> _logger;

    public SwitchItemsMessageHandler(ILogger<SwitchItemsMessageHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(IPlayerSession session, SwitchItemsMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        switch (message.InterfaceId)
        {
            case 149: // Main inventory
            {
                if (message.FromSlot < 0 || message.FromSlot >= player.Inventory.Capacity ||
                    message.ToSlot < 0 || message.ToSlot >= player.Inventory.Capacity)
                    return ValueTask.CompletedTask;

                player.Inventory.Swap(message.FromSlot, message.ToSlot);
                // TODO: session.RefreshInventory();
                break;
            }

            default:
                _logger.LogDebug("[{Username}] Unhandled SwitchItems interface: {InterfaceId}",
                    player.Username, message.InterfaceId);
                break;
        }

        return ValueTask.CompletedTask;
    }
}
