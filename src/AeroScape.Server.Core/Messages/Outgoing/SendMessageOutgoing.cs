namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Send a game/system message to the player's chatbox.
/// </summary>
public readonly record struct SendChatMessage(string Text);
