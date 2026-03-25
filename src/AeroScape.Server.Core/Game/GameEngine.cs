using AeroScape.Server.Core.Constants;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Main game loop — runs every 600ms (one game tick).
/// Processes movement, combat, etc. for all active players.
/// </summary>
public sealed class GameEngine : BackgroundService
{
    private readonly GameWorld _world;
    private readonly ILogger<GameEngine> _logger;

    public GameEngine(GameWorld world, ILogger<GameEngine> logger)
    {
        _world = world;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Game engine started — tick rate: {Rate}ms", ServerConstants.CycleRate);
        
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(ServerConstants.CycleRate));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                ProcessTick();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game tick");
            }
        }
    }

    private void ProcessTick()
    {
        var players = _world.GetActivePlayers().ToList();

        // Phase 1: Process movement for all players
        foreach (var player in players)
        {
            // Movement is processed by the MovementHandler attached to each session
            // TODO: Hook into session's MovementHandler.Process(player)
        }

        // Phase 2: Player updating (build update blocks)
        // TODO: Build and send player update packets

        // Phase 3: NPC updating
        // TODO: Build and send NPC update packets

        // Phase 4: Reset flags
        foreach (var player in players)
        {
            player.ResetFlags();
        }
    }
}
