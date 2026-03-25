using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles numeric input responses (e.g. "Enter amount" dialogs for banking/trading).
/// </summary>
public sealed class NumberInputHandler : IMessageHandler<NumberInputMessage>
{
    private readonly ILogger<NumberInputHandler> _logger;

    public NumberInputHandler(ILogger<NumberInputHandler> logger) => _logger = logger;

    public ValueTask HandleAsync(IPlayerSession session, NumberInputMessage message, CancellationToken ct)
    {
        _logger.LogTrace("Player {Name} entered number: {Value}",
            session.Player.Username, message.Value);

        // TODO: Route to the pending input consumer (bank withdraw X, trade offer X, etc.)
        // The player entity should track what dialog is awaiting input.
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handles string input responses (e.g. "Enter name" dialogs).
/// </summary>
public sealed class StringInputHandler : IMessageHandler<StringInputMessage>
{
    private readonly ILogger<StringInputHandler> _logger;

    public StringInputHandler(ILogger<StringInputHandler> logger) => _logger = logger;

    public ValueTask HandleAsync(IPlayerSession session, StringInputMessage message, CancellationToken ct)
    {
        _logger.LogTrace("Player {Name} entered string: {Value}",
            session.Player.Username, message.Value);

        // TODO: Route to the pending input consumer
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handles long/extended input responses.
/// </summary>
public sealed class LongInputHandler : IMessageHandler<LongInputMessage>
{
    private readonly ILogger<LongInputHandler> _logger;

    public LongInputHandler(ILogger<LongInputHandler> logger) => _logger = logger;

    public ValueTask HandleAsync(IPlayerSession session, LongInputMessage message, CancellationToken ct)
    {
        _logger.LogTrace("Player {Name} entered long: {Value} (inputId={InputId})",
            session.Player.Username, message.Value, message.InputId);

        return ValueTask.CompletedTask;
    }
}
