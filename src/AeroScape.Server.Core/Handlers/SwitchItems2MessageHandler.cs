using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles extended item switching — bank tab reorganization, bank insert-mode,
/// and inventory swaps while the bank interface is open.
/// Translated from legacy Java: DavidScape.io.packets.SwitchItems2
/// </summary>
public sealed class SwitchItems2MessageHandler : IMessageHandler<SwitchItems2Message>
{
    private readonly ILogger<SwitchItems2MessageHandler> _logger;

    public SwitchItems2MessageHandler(ILogger<SwitchItems2MessageHandler> logger)
    {
        _logger = logger;
    }

    // Magic constants from the 508 client
    private const int BankInterfaceId = 762;
    private const int BankInventoryInterfaceId = 763;
    private const int BankInventoryContainerRaw = 50003968;

    public ValueTask HandleAsync(IPlayerSession session, SwitchItems2Message message, CancellationToken ct = default)
    {
        var player = session.Player;

        // Disallow dragging items between bank and inventory containers directly
        if ((message.FromInterfaceRaw == BankInventoryContainerRaw ||
             message.ToInterfaceRaw == BankInventoryContainerRaw) &&
            message.FromInterfaceRaw != message.ToInterfaceRaw)
        {
            return ValueTask.CompletedTask;
        }

        switch (message.InterfaceId)
        {
            case BankInterfaceId:
                HandleBankSwitch(player, message);
                break;

            case BankInventoryInterfaceId:
            {
                // Inventory swap while bank is open
                if (message.FromSlot < 0 || message.FromSlot >= player.Inventory.Capacity ||
                    message.ToSlot < 0 || message.ToSlot >= player.Inventory.Capacity)
                    return ValueTask.CompletedTask;

                player.Inventory.Swap(message.FromSlot, message.ToSlot);
                // TODO: session.RefreshInventory();
                break;
            }

            default:
                _logger.LogDebug("[{Username}] Unhandled SwitchItems2 interface: {InterfaceId}",
                    player.Username, message.InterfaceId);
                break;
        }

        return ValueTask.CompletedTask;
    }

    private void HandleBankSwitch(Entities.Player player, SwitchItems2Message message)
    {
        switch (message.TabId)
        {
            case 73: // Swap/insert within the bank grid
            {
                if (message.FromSlot < 0 || message.FromSlot >= player.Bank.Capacity ||
                    message.ToSlot < 0 || message.ToSlot >= player.Bank.Capacity)
                    return;

                // TODO: When insert-mode is implemented, call Bank.Insert() instead of Swap()
                // For now, always swap
                player.Bank.Swap(message.FromSlot, message.ToSlot);
                // TODO: session.RefreshBank();
                break;
            }

            // Tab icon drags (25, 27, 29, 31, 33, 35, 37, 39, 41)
            // and direct-into-tab drags (51..59)
            case 25 or 27 or 29 or 31 or 33 or 35 or 37 or 39 or 41
                 or 51 or 52 or 53 or 54 or 55 or 56 or 57 or 58 or 59:
            {
                // TODO: Implement bank tab reorganization (insert item into target tab)
                // Requires tab start-slot tracking on the Player entity
                _logger.LogDebug("[{Username}] Bank tab drag not yet implemented (tab {TabId})",
                    player.Username, message.TabId);
                break;
            }

            default:
                _logger.LogDebug("[{Username}] Unhandled bank SwitchItems2 tabId: {TabId}",
                    player.Username, message.TabId);
                break;
        }
    }
}
