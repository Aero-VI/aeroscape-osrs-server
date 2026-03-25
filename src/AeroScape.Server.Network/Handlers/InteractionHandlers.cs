using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles NPC interaction (Talk-to, Attack, etc.)
/// </summary>
public sealed class NpcInteractHandler : IMessageHandler<NpcInteractMessage>
{
    private readonly GameWorld _world;
    private readonly CombatSystem _combat;
    private readonly ProtocolService _protocol;
    private readonly ILogger<NpcInteractHandler> _logger;

    public NpcInteractHandler(GameWorld world, CombatSystem combat, ProtocolService protocol, ILogger<NpcInteractHandler> logger)
    {
        _world = world;
        _combat = combat;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, NpcInteractMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;
        var npc = _world.GetNpc(message.NpcIndex);
        
        if (npc == null || !npc.IsActive)
        {
            _logger.LogTrace("NPC interact with invalid index {Index}", message.NpcIndex);
            return;
        }

        _logger.LogTrace("Player {Name} interacting with NPC {NpcId} (option {Option})",
            player.Username, npc.Id, message.OptionIndex);

        // Face the NPC
        player.FaceEntity(message.NpcIndex);

        switch (message.OptionIndex)
        {
            case 1: // First option (Talk-to / Attack for attackable NPCs)
                if (npc.CombatLevel > 0)
                {
                    // Attack
                    _combat.AttackNpc(player, npc);
                    player.PlayAnimation(422); // Punch animation
                    await PacketSender.SendMessage(ps, _protocol, $"You attack the {npc.Name}.", ct);
                }
                else
                {
                    // Talk-to
                    await PacketSender.SendMessage(ps, _protocol, $"{npc.Name}: Hello, adventurer!", ct);
                }
                break;

            case 2: // Second option (Attack for non-combat NPCs, or secondary action)
                if (npc.CombatLevel > 0)
                {
                    _combat.AttackNpc(player, npc);
                    player.PlayAnimation(422);
                    await PacketSender.SendMessage(ps, _protocol, $"You attack the {npc.Name}.", ct);
                }
                else
                {
                    await PacketSender.SendMessage(ps, _protocol, $"You interact with {npc.Name}.", ct);
                }
                break;

            default:
                await PacketSender.SendMessage(ps, _protocol, $"Nothing interesting happens.", ct);
                break;
        }
    }
}

/// <summary>
/// Handles object interaction (doors, banks, ladders, etc.)
/// </summary>
public sealed class ObjectInteractHandler : IMessageHandler<ObjectInteractMessage>
{
    private readonly ProtocolService _protocol;
    private readonly ILogger<ObjectInteractHandler> _logger;

    public ObjectInteractHandler(ProtocolService protocol, ILogger<ObjectInteractHandler> logger)
    {
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ObjectInteractMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        _logger.LogTrace("Player {Name} object interact: id={ObjId} at ({X},{Y}) option {Opt}",
            player.Username, message.ObjectId, message.X, message.Y, message.OptionIndex);

        // Face the object
        player.FacePosition(new Position(message.X, message.Y));

        // Bank booths
        if (BankService.IsBankBooth(message.ObjectId))
        {
            await BankService.OpenBank(ps, _protocol, ct);
            return;
        }

        // Ladders (common object IDs)
        if (message.ObjectId is 1746 or 1747 or 1748 or 2147 or 2148)
        {
            int newZ = message.OptionIndex == 1 ? player.Position.Z + 1 : player.Position.Z - 1;
            newZ = Math.Clamp(newZ, 0, 3);
            player.Position = new Position(player.Position.X, player.Position.Y, newZ);
            player.NeedsMapRegionUpdate = true;
            player.IsTeleporting = true;
            player.UpdateRequired = true;
            await PacketSender.SendMessage(ps, _protocol, $"You climb the ladder.", ct);
            return;
        }

        // Stairs
        if (message.ObjectId is 2113 or 2114 or 2118 or 2119)
        {
            int newZ = message.OptionIndex == 1 ? player.Position.Z + 1 : player.Position.Z - 1;
            newZ = Math.Clamp(newZ, 0, 3);
            player.Position = new Position(player.Position.X, player.Position.Y, newZ);
            player.NeedsMapRegionUpdate = true;
            player.IsTeleporting = true;
            player.UpdateRequired = true;
            await PacketSender.SendMessage(ps, _protocol, $"You walk up the stairs.", ct);
            return;
        }

        await PacketSender.SendMessage(ps, _protocol, $"Nothing interesting happens. (Object: {message.ObjectId})", ct);
    }
}

/// <summary>
/// Handles player-on-player interaction (Follow, Trade, etc.)
/// </summary>
public sealed class PlayerInteractHandler : IMessageHandler<PlayerInteractMessage>
{
    private readonly GameWorld _world;
    private readonly ProtocolService _protocol;
    private readonly ILogger<PlayerInteractHandler> _logger;

    public PlayerInteractHandler(GameWorld world, ProtocolService protocol, ILogger<PlayerInteractHandler> logger)
    {
        _world = world;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, PlayerInteractMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;
        var target = _world.GetPlayer(message.TargetIndex);

        if (target == null || !target.IsActive)
        {
            _logger.LogTrace("Player interact with invalid index {Index}", message.TargetIndex);
            return;
        }

        _logger.LogTrace("Player {Name} interacting with {Target} (option {Option})",
            player.Username, target.Username, message.OptionIndex);

        player.FaceEntity(message.TargetIndex + 32768); // Player indices are offset by 32768

        switch (message.OptionIndex)
        {
            case 1: // Follow
                // TODO: Implement follow logic
                break;
            case 2: // Trade
                // TODO: Implement trade request
                var msgDef = _protocol.GetOutgoingByName("SendMessage");
                if (msgDef != null)
                {
                    var pkt = new PacketBuilder();
                    pkt.WriteString($"Sending trade request to {target.Username}...");
                    await ps.SendPacketAsync(pkt.BuildVarByte(msgDef.Opcode, ps.OutgoingCipher), ct);
                }
                break;
        }
    }
}

/// <summary>
/// Handles ground item pickup.
/// </summary>
public sealed class GroundItemInteractHandler : IMessageHandler<GroundItemInteractMessage>
{
    private readonly GameWorld _world;
    private readonly ProtocolService _protocol;
    private readonly ILogger<GroundItemInteractHandler> _logger;

    public GroundItemInteractHandler(GameWorld world, ProtocolService protocol, ILogger<GroundItemInteractHandler> logger)
    {
        _world = world;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, GroundItemInteractMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;
        var pos = new Position(message.X, message.Y, player.Position.Z);

        var groundItem = _world.RemoveGroundItem(message.ItemId, pos, player.Username);
        if (groundItem == null)
        {
            // Try public items
            groundItem = _world.RemoveGroundItem(message.ItemId, pos);
        }

        if (groundItem != null)
        {
            var item = new Item(groundItem.ItemId, groundItem.Amount);
            if (player.Inventory.Add(item))
            {
                _logger.LogTrace("Player {Name} picked up ground item {Id} x{Amount}",
                    player.Username, groundItem.ItemId, groundItem.Amount);
                await PacketSender.SendInventory(ps, _protocol, ct);
            }
            else
            {
                // Inventory full, put it back
                _world.AddGroundItem(groundItem);
                var msgDef = _protocol.GetOutgoingByName("SendMessage");
                if (msgDef != null)
                {
                    var pkt = new PacketBuilder();
                    pkt.WriteString("You don't have enough inventory space.");
                    await ps.SendPacketAsync(pkt.BuildVarByte(msgDef.Opcode, ps.OutgoingCipher), ct);
                }
            }
        }
    }
}

/// <summary>
/// Handles dialogue continue button.
/// </summary>
public sealed class DialogueContinueHandler : IMessageHandler<DialogueContinueMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, DialogueContinueMessage message, CancellationToken ct)
    {
        // TODO: Advance dialogue state machine
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handles window focus changes.
/// </summary>
public sealed class FocusChangedHandler : IMessageHandler<FocusChangedMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, FocusChangedMessage message, CancellationToken ct)
    {
        // Client focus changed — could track for anti-AFK
        return ValueTask.CompletedTask;
    }
}
