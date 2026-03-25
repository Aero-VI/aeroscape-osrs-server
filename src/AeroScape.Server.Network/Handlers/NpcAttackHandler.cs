using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles dedicated NPC attack packets (distinct from NpcInteract option-based interaction).
/// Translated from legacy Equipment.java / NPCAttack.java.
/// </summary>
public sealed class NpcAttackHandler : IMessageHandler<NpcAttackMessage>
{
    private readonly GameWorld _world;
    private readonly CombatSystem _combat;
    private readonly ProtocolService _protocol;
    private readonly ILogger<NpcAttackHandler> _logger;

    public NpcAttackHandler(GameWorld world, CombatSystem combat, ProtocolService protocol, ILogger<NpcAttackHandler> logger)
    {
        _world = world;
        _combat = combat;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, NpcAttackMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;
        var npc = _world.GetNpc(message.NpcIndex);

        if (npc == null || !npc.IsActive)
        {
            _logger.LogTrace("NPC attack on invalid index {Index}", message.NpcIndex);
            return;
        }

        _logger.LogTrace("Player {Name} attacking NPC {NpcId} (index {Index})",
            player.Username, npc.Id, message.NpcIndex);

        // Face the NPC and initiate combat
        player.FaceEntity(message.NpcIndex);
        _combat.AttackNpc(player, npc);

        // Play default attack animation (punch)
        player.PlayAnimation(422);

        await PacketSender.SendMessage(ps, _protocol, $"You attack the {npc.Name}.", ct);
    }
}
