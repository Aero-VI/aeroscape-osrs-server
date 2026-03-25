using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles Barbarian Assault minigame actions — wave entry, NPC death tracking,
/// player death handling, and game completion.
/// Translated from legacy Java: DavidScape.io.packets.Assault
///
/// NOTE: The legacy implementation stored all game state as mutable fields on the
/// packet handler itself (a singleton-like pattern). In the new architecture, persistent
/// minigame state should live in a dedicated AssaultGameService registered in DI.
/// This handler delegates intent; the service manages the game lifecycle.
/// </summary>
public sealed class AssaultMessageHandler : IMessageHandler<AssaultMessage>
{
    private readonly ILogger<AssaultMessageHandler> _logger;

    public AssaultMessageHandler(ILogger<AssaultMessageHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Coordinates for wave lobbies (waiting rooms) indexed by wave (1-5).
    /// </summary>
    private static readonly Position[] WaveLobbyPositions =
    {
        new(2579, 5298, 0), // Wave 1
        new(2587, 5298, 0), // Wave 2
        new(2599, 5298, 0), // Wave 3
        new(2607, 5298, 0), // Wave 4
        new(2579, 5288, 0), // Wave 5
    };

    /// <summary>
    /// Coordinates players are teleported to when leaving the game / wave ends.
    /// </summary>
    private static readonly Position[] WaveExitPositions =
    {
        new(2579, 5299, 0), // Wave 1
        new(2587, 5299, 0), // Wave 2
        new(2599, 5299, 0), // Wave 3
        new(2607, 5299, 0), // Wave 4
        new(2579, 5289, 0), // Wave 5
    };

    /// <summary>
    /// Arena spawn origin — players teleported here on wave start (height varies by game instance).
    /// </summary>
    private static readonly Position ArenaOrigin = new(1886, 5472, 0);

    /// <summary>
    /// NPC counts per wave (wave 1-5).
    /// </summary>
    private static readonly int[] NpcsPerWave = { 2, 4, 6, 8, 10 };

    /// <summary>
    /// NPC HP per wave.
    /// </summary>
    private static readonly int[] HpPerWave = { 25, 40, 65, 80, 100 };

    /// <summary>
    /// NPC max hit per wave.
    /// </summary>
    private static readonly int[] MaxHitPerWave = { 5, 9, 13, 15, 18 };

    /// <summary>
    /// Healer NPC IDs per wave.
    /// </summary>
    public static readonly int[] HealerIds = { 5238, 5239, 5240, 5241, 5242 };

    /// <summary>
    /// Ranger NPC IDs per wave.
    /// </summary>
    public static readonly int[] RangerIds = { 5229, 5230, 5231, 5232, 5233 };

    /// <summary>
    /// Fighter NPC IDs per wave.
    /// </summary>
    public static readonly int[] FighterIds = { 5044, 5045, 5213, 5214, 5215 };

    /// <summary>
    /// Runner NPC IDs per wave.
    /// </summary>
    public static readonly int[] RunnerIds = { 5220, 5221, 5222, 5223, 5224 };

    /// <summary>
    /// Required players to start a wave.
    /// </summary>
    private const int RequiredPlayers = 3;

    public ValueTask HandleAsync(IPlayerSession session, AssaultMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        switch (message.Action)
        {
            case AssaultAction.EnterWave:
                HandleEnterWave(session, player, message.Wave);
                break;

            case AssaultAction.NpcDied:
                // TODO: Delegate to AssaultGameService to track kill counts
                // and check wave completion (healers/rangers/fighters/runners).
                _logger.LogDebug("Assault NPC died in wave {Wave}", message.Wave);
                break;

            case AssaultAction.PlayerDied:
                // TODO: Delegate to AssaultGameService — teleport all participants
                // to their wave exit positions and end the game.
                _logger.LogDebug("Assault player died in wave {Wave}", message.Wave);
                break;

            case AssaultAction.EndGame:
                // TODO: Delegate to AssaultGameService — force-end game, clear participants.
                _logger.LogDebug("Assault game force-ended for wave {Wave}", message.Wave);
                break;
        }

        return ValueTask.CompletedTask;
    }

    private void HandleEnterWave(IPlayerSession session, Player player, int wave)
    {
        if (wave < 1 || wave > 5)
            return;

        // TODO: These fields (WaveId, InAssault, IsWaiting, Rewards) need to be added
        // to Player entity when the Barbarian Assault minigame is fully implemented.
        //
        // Legacy logic summary:
        // 1. Validate player's current wave >= requested wave (downgrade allowed)
        // 2. Toggle waiting state (enter lobby / leave lobby)
        // 3. Teleport to lobby or exit position
        // 4. Count waiting players for that wave
        // 5. If >= RequiredPlayers and no game running → start wave, teleport all to arena
        // 6. On wave completion (all 4 NPC types dead) → advance wave or award rewards at wave 5

        _logger.LogDebug("[{Username}] Barbarian Assault enter wave {Wave} requested",
            player.Username, wave);

        // Lobby position (0-indexed)
        var lobbyPos = WaveLobbyPositions[wave - 1];
        var exitPos = WaveExitPositions[wave - 1];

        // TODO: Full implementation requires AssaultGameService with:
        // - Wave state tracking (spawned NPCs, kill counts per type)
        // - Player waiting list management
        // - Instance height-level isolation (gamen * 4)
        // - NPC spawning via NpcService
        // - Wave completion → reward point tracking
    }

    /// <summary>
    /// Determines which NPC category (healer/ranger/fighter/runner) an NPC ID belongs to.
    /// Returns null if the ID is not a Barbarian Assault NPC.
    /// </summary>
    public static string? GetNpcCategory(int npcId)
    {
        if (Array.Exists(HealerIds, id => id == npcId)) return "Healer";
        if (Array.Exists(RangerIds, id => id == npcId)) return "Ranger";
        if (Array.Exists(FighterIds, id => id == npcId)) return "Fighter";
        if (Array.Exists(RunnerIds, id => id == npcId)) return "Runner";
        return null;
    }
}
