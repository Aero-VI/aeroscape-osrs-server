namespace AeroScape.Server.Core.Messages;

/// <summary>
/// A prayer toggle request from the prayer interface.
/// ButtonId is the raw interface button (odd values 5..57 map to prayer indices 0..26).
/// </summary>
public readonly record struct PrayerMessage(int ButtonId);
