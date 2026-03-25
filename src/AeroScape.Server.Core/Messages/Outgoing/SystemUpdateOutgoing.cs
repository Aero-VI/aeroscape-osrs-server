namespace AeroScape.Server.Core.Messages.Outgoing;

/// <summary>
/// Display the "System update in X seconds" countdown.
/// </summary>
public readonly record struct SystemUpdateMessage(int CountdownTicks);
