namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Accept a trade request from another player.
/// </summary>
public readonly record struct TradeAcceptMessage(int TargetIndex);
