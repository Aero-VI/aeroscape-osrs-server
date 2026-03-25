namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Player appearance change (character design screen).
/// </summary>
public readonly record struct AppearanceUpdateMessage(int Gender, int[] Look, int[] Colors);
