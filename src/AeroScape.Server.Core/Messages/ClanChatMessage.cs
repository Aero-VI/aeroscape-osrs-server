namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Clan chat packet — placeholder for the 508 clan chat system.
/// The legacy Java class (DavidScape.io.packets.ClanChat) was an empty stub,
/// so this message exists to register the packet in the decode pipeline and
/// provide a hook for future clan chat implementation.
///
/// Actual clan-specific messages (join, leave, kick, message, set rank)
/// should use dedicated message types or extend this with an action enum
/// when the clan system is built out.
/// </summary>
public readonly record struct ClanChatMessage(ClanChatAction Action, string Text = "");

/// <summary>
/// Clan chat actions.
/// </summary>
public enum ClanChatAction
{
    /// <summary>Join a clan chat channel.</summary>
    Join,

    /// <summary>Leave the current clan chat channel.</summary>
    Leave,

    /// <summary>Send a message to the clan chat channel.</summary>
    SendMessage,

    /// <summary>Kick a player from the clan chat channel.</summary>
    Kick,
}
