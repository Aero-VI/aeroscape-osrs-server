using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles ActionButtons packets — the player clicked a button on a game interface.
/// Routes the button press to the appropriate subsystem based on the interface id
/// (prayers, combat styles, emotes, magic book, settings, bank, shops, trade, etc.).
///
/// Translated from legacy Java: DavidScape.io.packets.ActionButtons
/// </summary>
public sealed class ActionButtonsMessageHandler : IMessageHandler<ActionButtonsMessage>
{
    private readonly ILogger<ActionButtonsMessageHandler> _logger;

    public ActionButtonsMessageHandler(ILogger<ActionButtonsMessageHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(IPlayerSession session, ActionButtonsMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        // TODO: Guard — if action button timer is active, ignore input
        // TODO: Guard — if player is jailed, reject with message

        switch (message.InterfaceId)
        {
            // ── Clan Chat interfaces ─────────────────────────────────
            case 589: // Clan chat setup (toggle loot share, open settings)
            case 590: // Clan settings (set prefix, disable chat)
                // TODO: Delegate to ClanChatService
                break;

            // ── Construction ─────────────────────────────────────────
            case 402: // Room selection
                // TODO: Delegate to ConstructionService.AddRoom(buttonId - 160)
                break;

            // ── Smithing ─────────────────────────────────────────────
            case 300: // Smithing interface
                // TODO: Delegate to SmithingService.Smith(player.SmithType, 1, buttonId)
                break;

            // ── Runecrafting altar teleports ──────────────────────────
            case 45:
                // TODO: Route altar teleports based on buttonId (87-99)
                break;

            // ── Shop interfaces ──────────────────────────────────────
            case 620:
            case 621:
                // TODO: Delegate to ShopService.HandleOption(interfaceId, buttonId, buttonId2, packetId)
                break;

            // ── Skill goal chooser ───────────────────────────────────
            case 134:
                // TODO: Map buttonId (29-52) to chosen skill index
                break;

            // ── Quest log / teleport panel ────────────────────────────
            case 274:
                // TODO: Donor-only quest journal / teleport destinations
                break;

            // ── NPC dialogue / cooking / smelting option menus ────────
            case 458:
                // TODO: Multi-choice dialogue handler (1 / 5 / All variants)
                break;

            // ── Makeover (male / female / clothing) ──────────────────
            case 596: // Male hair/beard
            case 592: // Female hair
            case 591: // Male clothing (torso, arms, legs)
                // TODO: Delegate to MakeoverService
                break;

            // ── Friends list ─────────────────────────────────────────
            case 751:
                // TODO: Open friends/ignore interface
                break;

            // ── Skills tab (teleport to training areas) ──────────────
            case 320:
                // TODO: Teleport to skill training area by buttonId (125-148)
                break;

            // ── Skill info pages ─────────────────────────────────────
            case 499:
                // TODO: Set config for skill menu pagination
                break;

            // ── Summoning familiar interface ─────────────────────────
            case 663:
                // TODO: Dismiss (23) / Call (21) familiar
                break;

            // ── Magic spellbooks ─────────────────────────────────────
            case 430: // Lunar
            case 192: // Modern
            case 193: // Ancient
                // TODO: Delegate to MagicService for autocast / spell selection
                break;

            // ── Autocast selection ────────────────────────────────────
            case 319:
                // TODO: Set autocast spell from the modern spellbook panel
                break;

            // ── Equipment tab ────────────────────────────────────────
            case 387:
                // TODO: Open equipment stats screen (buttonId == 55)
                break;

            // ── Magic on item / magic AoP ────────────────────────────
            case 154:
            case 70:
                // TODO: Delegate to MagicService
                break;

            // ── Character design ─────────────────────────────────────
            case 771:
                // TODO: Delegate to CharacterDesignService
                break;

            // ── House options (building mode, expel, leave) ──────────
            case 398:
                // TODO: Delegate to ConstructionService
                break;

            // ── Run / settings ────────────────────────────────────────
            case 750:
            case 261:
                // TODO: Toggle run, open brightness/audio settings, house settings tab
                break;

            // ── Trading interfaces ───────────────────────────────────
            case 334:
            case 335:
            case 336:
                // TODO: Delegate to TradeService
                break;

            // ── Prayer tab ───────────────────────────────────────────
            case 271:
                // TODO: Delegate to PrayerService
                break;

            // ── Emotes tab ───────────────────────────────────────────
            case 464:
                // TODO: Delegate to EmoteService (play animation + GFX)
                break;

            // ── Combat style tabs (weapon interfaces) ────────────────
            case 75:  // Dragon battleaxe special
            case 76: case 77: case 78: case 79:
            case 81: case 82: case 83: case 84:
            case 85: case 86: case 87: case 88:
            case 89: case 90: case 91: case 92: case 93:
                // TODO: Set attack style, toggle special, toggle auto-retaliate
                break;

            // ── Welcome / login screen ───────────────────────────────
            case 378:
                // TODO: Set window pane on "click to play"
                break;

            // ── Logout ───────────────────────────────────────────────
            case 182:
                // TODO: Broadcast logout message, initiate logout sequence
                session.Disconnect("Logout requested");
                break;

            // ── Banking ──────────────────────────────────────────────
            case 762: // Bank main
            case 763: // Bank inventory overlay
                // TODO: Delegate to BankService (deposit/withdraw with quantity from packetId)
                break;

            default:
                _logger.LogDebug("[{Username}] Unhandled ActionButton: interface={InterfaceId}, button={ButtonId}:{ButtonId2}",
                    player.Username, message.InterfaceId, message.ButtonId, message.ButtonId2);
                break;
        }

        return ValueTask.CompletedTask;
    }
}
