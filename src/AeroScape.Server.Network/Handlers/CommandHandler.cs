using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

public sealed class CommandHandler : IMessageHandler<CommandMessage>
{
    private readonly ProtocolService _protocol;
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(ProtocolService protocol, ILogger<CommandHandler> logger)
    {
        _protocol = protocol;
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
                    // TODO: Send inventory update packet
                }
                break;

            case "master":
                for (int i = 0; i < SkillSet.SkillCount; i++)
                {
                    player.Skills.SetLevel(i, 99);
                    player.Skills.SetExperience(i, 13_034_431);
                }
                await SendMessage(ps, "All skills set to 99.", ct);
                // TODO: Send skill update packets
                break;

            case "pos":
                await SendMessage(ps, $"Position: {player.Position}", ct);
                break;

            case "players":
                await SendMessage(ps, $"Online players: {/* TODO: world count */ 0}", ct);
                break;

            default:
                await SendMessage(ps, $"Unknown command: ::{message.Command}", ct);
                break;
        }
    }

    private async Task SendMessage(PlayerSession session, string text, CancellationToken ct)
    {
        var def = _protocol.GetOutgoingByName("SendMessage");
        if (def == null) return;
        
        var pkt = new PacketBuilder();
        pkt.WriteString(text);
        await session.SendPacketAsync(pkt.BuildVarByte(def.Opcode, session.OutgoingCipher), ct);
    }
}
