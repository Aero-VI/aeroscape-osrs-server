using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles settings/config button changes (brightness, music volume, auto-retaliate, etc.).
/// </summary>
public sealed class SettingsButtonHandler : IMessageHandler<SettingsButtonMessage>
{
    private readonly ILogger<SettingsButtonHandler> _logger;

    public SettingsButtonHandler(ILogger<SettingsButtonHandler> logger) => _logger = logger;

    public ValueTask HandleAsync(IPlayerSession session, SettingsButtonMessage message, CancellationToken ct)
    {
        _logger.LogTrace("Player {Name} changed setting {Id} to {Value}",
            session.Player.Username, message.SettingId, message.Value);

        // TODO: Persist player settings, apply auto-retaliate, run mode, etc.
        return ValueTask.CompletedTask;
    }
}
