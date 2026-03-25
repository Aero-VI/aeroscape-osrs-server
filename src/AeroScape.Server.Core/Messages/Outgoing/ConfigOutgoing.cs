namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Set a client config/varp value (e.g. prayer, attack style, brightness).
/// </summary>
public readonly record struct SendConfigMessage(int ConfigId, int Value);
