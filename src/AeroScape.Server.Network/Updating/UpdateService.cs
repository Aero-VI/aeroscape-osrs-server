using AeroScape.Server.Core.Game;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.Logging;

// NpcMovementService is in Core.Game
namespace AeroScape.Server.Network.Updating;

/// <summary>
/// Coordinates the player/NPC update cycle each game tick.
/// Called from the game engine after movement processing.
/// </summary>
public sealed class UpdateService : IGameTickProcessor
{
    private readonly PlayerSessionManager _sessionManager;
    private readonly GameWorld _world;
    private readonly ProtocolService _protocol;
    private readonly CombatSystem _combat;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        PlayerSessionManager sessionManager,
        GameWorld world,
        ProtocolService protocol,
        CombatSystem combat,
        ILogger<UpdateService> logger)
    {
        _sessionManager = sessionManager;
        _world = world;
        _protocol = protocol;
        _combat = combat;
        _logger = logger;
    }

    /// <summary>
    /// Processes movement for all players, then sends player and NPC update packets.
    /// </summary>
    public async Task ProcessTickAsync(CancellationToken ct)
    {
        var sessions = _sessionManager.GetAll().ToList();

        // Phase 1a: Process player movement
        foreach (var session in sessions)
        {
            if (!session.IsConnected) continue;
            session.Movement.Process(session.Player);
        }

        // Phase 1b: Process NPC movement (random walking)
        NpcMovementService.ProcessAll(_world);

        // Phase 1c: Process combat
        _combat.ProcessTick();

        // Phase 2: Send map region updates if needed
        foreach (var session in sessions)
        {
            if (!session.IsConnected) continue;
            if (session.Player.NeedsMapRegionUpdate)
            {
                await SendMapRegionAsync(session, ct);
                session.Player.NeedsMapRegionUpdate = false;
                session.Player.LastKnownRegion = session.Player.Position;
            }
        }

        // Phase 3: Build and send player updates
        foreach (var session in sessions)
        {
            if (!session.IsConnected) continue;
            try
            {
                var playerUpdateData = PlayerUpdatePacket.Build(session, _protocol);
                if (playerUpdateData.Length > 0)
                    await session.SendPacketAsync(playerUpdateData, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending player update to {Player}", session.Player.Username);
            }
        }

        // Phase 4: Build and send NPC updates
        foreach (var session in sessions)
        {
            if (!session.IsConnected) continue;
            try
            {
                var npcUpdateData = NpcUpdatePacket.Build(session, _world, _protocol);
                if (npcUpdateData.Length > 0)
                    await session.SendPacketAsync(npcUpdateData, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending NPC update to {Player}", session.Player.Username);
            }
        }

        // Phase 5: Reset flags
        foreach (var player in _world.GetActivePlayers())
            player.ResetFlags();

        foreach (var npc in _world.GetActiveNpcs())
            npc.ResetFlags();

        // Phase 6: Tick ground items
        _world.TickGroundItems();
    }

    private async Task SendMapRegionAsync(PlayerSession session, CancellationToken ct)
    {
        var player = session.Player;
        var def = _protocol.GetOutgoingByName("MapRegion");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteShortA(player.Position.RegionX);
        pkt.WriteShort(player.Position.RegionY);
        await session.SendPacketAsync(pkt.BuildVarShort(def.Opcode, session.OutgoingCipher), ct);
    }
}
