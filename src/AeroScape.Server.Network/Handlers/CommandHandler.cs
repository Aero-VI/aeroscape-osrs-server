using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

public sealed class CommandHandler : IMessageHandler<CommandMessage>
{
    private readonly ProtocolService _protocol;
    private readonly GameWorld _world;
    private readonly ItemDefinitionService _itemDefs;
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(ProtocolService protocol, GameWorld world, ItemDefinitionService itemDefs, ILogger<CommandHandler> logger)
    {
        _protocol = protocol;
        _world = world;
        _itemDefs = itemDefs;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, CommandMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        _logger.LogInformation("Command from {Player}: ::{Command} {Args}",
            player.Username, message.Command, string.Join(" ", message.Arguments));

        switch (message.Command)
        {
            case "tele" when message.Arguments.Length >= 2:
                if (int.TryParse(message.Arguments[0], out int x) &&
                    int.TryParse(message.Arguments[1], out int y))
                {
                    int z = message.Arguments.Length >= 3 && int.TryParse(message.Arguments[2], out int zz) ? zz : 0;
                    player.Position = new Position(x, y, z);
                    player.NeedsMapRegionUpdate = true;
                    player.IsTeleporting = true;
                    player.UpdateRequired = true;
                    await SendMessage(ps, $"Teleported to ({x}, {y}, {z})", ct);
                }
                break;

            case "item" when message.Arguments.Length >= 1:
                if (int.TryParse(message.Arguments[0], out int itemId))
                {
                    int amount = message.Arguments.Length >= 2 && int.TryParse(message.Arguments[1], out int a) ? a : 1;
                    player.Inventory.Add(new Item(itemId, amount));
                    await SendMessage(ps, $"Spawned item {itemId} x{amount}", ct);
                    await SendInventoryUpdate(ps, ct);
                }
                break;

            case "master":
                for (int i = 0; i < SkillSet.SkillCount; i++)
                {
                    player.Skills.SetLevel(i, 99);
                    player.Skills.SetExperience(i, 13_034_431);
                }
                await SendMessage(ps, "All skills set to 99.", ct);
                await SendAllSkills(ps, ct);
                break;

            case "pos":
                await SendMessage(ps, $"Position: {player.Position}", ct);
                break;

            case "players":
                await SendMessage(ps, $"Online players: {_world.PlayerCount}", ct);
                break;

            case "npc" when message.Arguments.Length >= 1:
                if (int.TryParse(message.Arguments[0], out int npcId))
                {
                    var npc = new Npc(npcId, player.Position)
                    {
                        Name = $"NPC-{npcId}",
                        CurrentHealth = 100,
                        MaxHealth = 100
                    };
                    _world.RegisterNpc(npc);
                    await SendMessage(ps, $"Spawned NPC {npcId} at your position.", ct);
                }
                break;

            case "anim" when message.Arguments.Length >= 1:
                if (int.TryParse(message.Arguments[0], out int animId))
                {
                    player.PlayAnimation(animId);
                    await SendMessage(ps, $"Playing animation {animId}.", ct);
                }
                break;

            case "gfx" when message.Arguments.Length >= 1:
                if (int.TryParse(message.Arguments[0], out int gfxId))
                {
                    int height = message.Arguments.Length >= 2 && int.TryParse(message.Arguments[1], out int h) ? h : 100;
                    player.PlayGraphic(gfxId, height);
                    await SendMessage(ps, $"Playing graphic {gfxId}.", ct);
                }
                break;

            case "bank":
                // TODO: Open bank interface
                await SendMessage(ps, "Banking not yet implemented.", ct);
                break;

            case "empty":
                player.Inventory.Clear();
                await SendInventoryUpdate(ps, ct);
                await SendMessage(ps, "Inventory cleared.", ct);
                break;

            case "heal":
                player.Skills.SetLevel(3, player.Skills.GetLevelForExperience(player.Skills.GetExperience(3)));
                await SendSkill(ps, 3, ct);
                await SendMessage(ps, "Healed to full.", ct);
                break;

            case "setlevel" when message.Arguments.Length >= 2:
                if (int.TryParse(message.Arguments[0], out int skillId) &&
                    int.TryParse(message.Arguments[1], out int level) &&
                    skillId >= 0 && skillId < SkillSet.SkillCount && level >= 1 && level <= 99)
                {
                    player.Skills.SetLevel(skillId, level);
                    // Calculate XP for the level
                    int xp = 0;
                    int pts = 0;
                    for (int i = 1; i < level; i++)
                    {
                        pts += (int)(i + 300.0 * Math.Pow(2.0, i / 7.0));
                        xp = pts / 4;
                    }
                    player.Skills.SetExperience(skillId, xp);
                    await SendSkill(ps, skillId, ct);
                    await SendMessage(ps, $"Set {SkillSet.SkillNames[skillId]} to level {level}.", ct);
                }
                break;

            case "energy":
                player.RunEnergy = 100;
                await SendEnergy(ps, ct);
                await SendMessage(ps, "Run energy restored.", ct);
                break;

            case "yell" when message.Arguments.Length >= 1:
                string yellMsg = string.Join(" ", message.Arguments);
                // Broadcast to all players
                foreach (var otherSession in ps.SessionManager?.GetAll() ?? [])
                {
                    await SendMessageTo(otherSession, $"[{player.Username}]: {yellMsg}", ct);
                }
                break;

            default:
                await SendMessage(ps, $"Unknown command: ::{message.Command}", ct);
                break;
        }
    }

    private async Task SendMessage(PlayerSession session, string text, CancellationToken ct) =>
        await SendMessageTo(session, text, ct);

    private async Task SendMessageTo(PlayerSession session, string text, CancellationToken ct)
    {
        var def = _protocol.GetOutgoingByName("SendMessage");
        if (def == null) return;
        
        var pkt = new PacketBuilder();
        pkt.WriteString(text);
        await session.SendPacketAsync(pkt.BuildVarByte(def.Opcode, session.OutgoingCipher), ct);
    }

    private async Task SendInventoryUpdate(PlayerSession session, CancellationToken ct)
    {
        var def = _protocol.GetOutgoingByName("SetItems");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteShort(3214); // inventory interface
        pkt.WriteShort(session.Player.Inventory.Capacity);
        for (int i = 0; i < session.Player.Inventory.Capacity; i++)
        {
            var item = session.Player.Inventory.Get(i);
            if (item != null)
            {
                if (item.Amount > 254)
                {
                    pkt.WriteByte(255);
                    pkt.WriteInt(item.Amount);
                }
                else
                {
                    pkt.WriteByte(item.Amount);
                }
                pkt.WriteLEShortA(item.Id + 1);
            }
            else
            {
                pkt.WriteByte(0);
                pkt.WriteLEShortA(0);
            }
        }
        await session.SendPacketAsync(pkt.BuildVarShort(def.Opcode, session.OutgoingCipher), ct);
    }

    private async Task SendAllSkills(PlayerSession session, CancellationToken ct)
    {
        for (int i = 0; i < SkillSet.SkillCount; i++)
            await SendSkill(session, i, ct);
    }

    private async Task SendSkill(PlayerSession session, int skillId, CancellationToken ct)
    {
        var def = _protocol.GetOutgoingByName("SendSkill");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteByte(skillId);
        pkt.WriteInt(session.Player.Skills.GetExperience(skillId));
        pkt.WriteByte(session.Player.Skills.GetLevel(skillId));
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }

    private async Task SendEnergy(PlayerSession session, CancellationToken ct)
    {
        var def = _protocol.GetOutgoingByName("SendEnergy");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteByte(session.Player.RunEnergy);
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }
}
