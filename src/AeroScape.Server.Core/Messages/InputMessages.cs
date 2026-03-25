namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Numeric input response (e.g., "Enter amount").
/// </summary>
public readonly record struct NumberInputMessage(int Value);

/// <summary>
/// String input response (e.g., "Enter name").
/// </summary>
public readonly record struct StringInputMessage(string Value);

/// <summary>
/// Long/extended input response.
/// </summary>
public readonly record struct LongInputMessage(long Value, int InputId);
