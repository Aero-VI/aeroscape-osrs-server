using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles ItemOperate packets — the player right-clicked "Operate" on an equipped item.
/// Validates the item exists in the given equipment slot, then delegates to
/// item-specific operate logic (teleport jewellery, DFS special, etc.).
///
/// Translated from legacy Java: DavidScape.io.packets.ItemOperate
/// </summary>
public sealed class ItemOperateMessageHandler : IMessageHandler<ItemOperateMessage>
{
    private readonly ILogger<ItemOperateMessageHandler> _logger;

    // ── Known operable item IDs ──────────────────────────────────────
    private const int GloryAmulet = 1704;
    private const int DragonFireShield = 11283;

    public ItemOperateMessageHandler(ILogger<ItemOperateMessageHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(IPlayerSession session, ItemOperateMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        // Validate the equipment slot bounds and that the item actually matches
        if (message.Slot < 0 || message.Slot >= player.Equipment.Capacity)
            return ValueTask.CompletedTask;

        var equippedItem = player.Equipment.Get(message.Slot);
        if (equippedItem?.Id != message.ItemId)
            return ValueTask.CompletedTask;

        switch (message.ItemId)
        {
            case GloryAmulet:
                // TODO: Open teleport choice dialogue (Fight Pits / Castle Wars / Port Sarim)
                //   Legacy opened interface 458 with three options and set Choice = 3.
                _logger.LogDebug("[{Username}] Operate: Glory amulet teleport menu", player.Username);
                break;

            case DragonFireShield:
                // TODO: DFS special attack
                //   - Check cooldown timer (DFStimer == 0)
                //   - Require player to be in combat (attackingPlayer)
                //   - Play animation 6695, GFX 1164
                //   - Send projectile 1166 toward target
                //   - Deal up to 50 damage
                //   - Set cooldown (DFStimer += 10)
                _logger.LogDebug("[{Username}] Operate: Dragon fire shield special", player.Username);
                break;

            default:
                _logger.LogDebug("[{Username}] Unhandled item operate: itemId={ItemId}, slot={Slot}",
                    player.Username, message.ItemId, message.Slot);
                break;
        }

        return ValueTask.CompletedTask;
    }
}
