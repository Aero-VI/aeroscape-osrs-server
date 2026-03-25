using AeroScape.Server.Core.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Main game loop — runs every 600ms (one game tick).
/// Delegates update cycle to the network layer's UpdateService.
/// </summary>
public sealed class GameEngine : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameEngine> _logger;

    public GameEngine(IServiceProvider serviceProvider, ILogger<GameEngine> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Game engine started — tick rate: {Rate}ms", ServerConstants.CycleRate);
        
        // Resolve the update processor (registered by the network layer)
        var tickProcessor = _serviceProvider.GetService<IGameTickProcessor>();
        
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(ServerConstants.CycleRate));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (tickProcessor != null)
                    await tickProcessor.ProcessTickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game tick");
            }
        }
    }
}

/// <summary>
/// Interface for the network layer to provide tick processing without the core
/// depending on the network layer directly.
/// </summary>
public interface IGameTickProcessor
{
    Task ProcessTickAsync(CancellationToken ct);
}
