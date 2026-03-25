using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles adding friends.
/// </summary>
public sealed class AddFriendHandler : IMessageHandler<AddFriendMessage>
{
    private readonly GameWorld _world;
    private readonly ProtocolService _protocol;
    private readonly ILogger<AddFriendHandler> _logger;

    public AddFriendHandler(GameWorld world, ProtocolService protocol, ILogger<AddFriendHandler> logger)
    {
        _world = world;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, AddFriendMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        if (player.FriendsList.Count >= 200)
        {
            await PacketSender.SendMessage(ps, _protocol, "Your friend list is full.", ct);
            return;
        }

        if (player.FriendsList.Contains(message.FriendNameLong))
            return; // Already added

        player.FriendsList.Add(message.FriendNameLong);

        // Send friend online status
        var friendName = PlayerUpdatePacket.LongToName(message.FriendNameLong);
        bool online = _world.IsOnline(friendName);

        _logger.LogTrace("Player {Name} added friend: {Friend} (online={Online})",
            player.Username, friendName, online);

        // Send friend status packet
        await SendFriendStatus(ps, message.FriendNameLong, online ? 1 : 0, ct);
    }

    private async Task SendFriendStatus(PlayerSession session, long nameLong, int world, CancellationToken ct)
    {
        var def = _protocol.GetOutgoingByName("FriendStatus");
        if (def == null) return;

        var pkt = new PacketBuilder();
        pkt.WriteLong(nameLong);
        pkt.WriteByte(world); // 0 = offline, 1+ = world number
        await session.SendPacketAsync(pkt.Build(def.Opcode, session.OutgoingCipher), ct);
    }
}

/// <summary>
/// Handles removing friends.
/// </summary>
public sealed class RemoveFriendHandler : IMessageHandler<RemoveFriendMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, RemoveFriendMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        ps.Player.FriendsList.Remove(message.FriendNameLong);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handles adding to ignore list.
/// </summary>
public sealed class AddIgnoreHandler : IMessageHandler<AddIgnoreMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, AddIgnoreMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        if (ps.Player.IgnoreList.Count >= 100) return ValueTask.CompletedTask;
        if (!ps.Player.IgnoreList.Contains(message.IgnoreNameLong))
            ps.Player.IgnoreList.Add(message.IgnoreNameLong);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handles removing from ignore list.
/// </summary>
public sealed class RemoveIgnoreHandler : IMessageHandler<RemoveIgnoreMessage>
{
    public ValueTask HandleAsync(IPlayerSession session, RemoveIgnoreMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return ValueTask.CompletedTask;
        ps.Player.IgnoreList.Remove(message.IgnoreNameLong);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Handles sending private messages.
/// </summary>
public sealed class PrivateMessageHandler : IMessageHandler<PrivateMessageMessage>
{
    private readonly GameWorld _world;
    private readonly PlayerSessionManager _sessionManager;
    private readonly ProtocolService _protocol;
    private readonly ILogger<PrivateMessageHandler> _logger;

    public PrivateMessageHandler(GameWorld world, PlayerSessionManager sessionManager, 
        ProtocolService protocol, ILogger<PrivateMessageHandler> logger)
    {
        _world = world;
        _sessionManager = sessionManager;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, PrivateMessageMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;
        var player = ps.Player;

        var recipientName = PlayerUpdatePacket.LongToName(message.RecipientNameLong);
        var recipientPlayer = _world.FindPlayer(recipientName);

        if (recipientPlayer == null || !recipientPlayer.IsActive)
        {
            await PacketSender.SendMessage(ps, _protocol, "That player is not online.", ct);
            return;
        }

        // Find recipient session
        foreach (var recipientSession in _sessionManager.GetAll())
        {
            if (recipientSession.Player == recipientPlayer)
            {
                // Check if sender is on recipient's ignore list
                var senderNameLong = PlayerUpdatePacket.NameToLong(player.Username);
                if (recipientPlayer.IgnoreList.Contains(senderNameLong))
                    return; // Silently drop

                // Send PM to recipient
                var def = _protocol.GetOutgoingByName("ReceivePrivateMessage");
                if (def != null)
                {
                    var pkt = new PacketBuilder();
                    pkt.WriteLong(senderNameLong);
                    pkt.WriteInt(Random.Shared.Next()); // unique message id
                    pkt.WriteByte(player.Rights);
                    pkt.WriteBytes(message.PackedText);
                    await recipientSession.SendPacketAsync(pkt.BuildVarByte(def.Opcode, recipientSession.OutgoingCipher), ct);
                }

                _logger.LogTrace("PM from {Sender} to {Recipient}", player.Username, recipientName);
                break;
            }
        }
    }
}
