namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Public chat message with color, effect, and packed chat text.
/// </summary>
public readonly record struct PublicChatMessage(int Color, int Effect, byte[] PackedText);

/// <summary>
/// A player-issued command (e.g. ::item 4151 1).
/// </summary>
public readonly record struct CommandMessage(string Command, string[] Arguments);
