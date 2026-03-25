using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles ActionButtons packets — the player clicked a button on a game interface.
/// Routes the button press to the appropriate subsystem based on the interface id
/// (prayers, combat styles, emotes, magic book, settings, etc.).
/// </summary>
public sealed class ActionButtonsMessageHandler : IMessageHandler<ActionButtonsMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, ActionButtonsMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        // TODO: Route by InterfaceId to the correct subsystem:
        //   - Prayer interface: toggle prayer on/off, check prayer level
        //   - Combat styles tab: switch attack style (accurate, aggressive, defensive, controlled)
        //   - Magic spellbook: select autocast spell
        //   - Emotes tab: play emote animation
        //   - Settings tab: toggle run/walk, brightness, music volume, etc.
        //   - Equipment stats: open equipment screen
        //   - Logout button: initiate logout sequence
        //   - Special attack bar: toggle special attack

        // Log for debugging during development
        // player.SendMessage($"ActionButton: interface={message.InterfaceId}, button={message.ButtonId}");

        return ValueTask.CompletedTask;
    }
}
