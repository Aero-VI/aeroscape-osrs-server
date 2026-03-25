using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Pipeline;

/// <summary>
/// Decodes raw packet bytes into protocol-agnostic message records,
/// then resolves and invokes the appropriate IMessageHandler&lt;T&gt; from DI.
/// </summary>
public sealed class PacketDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PacketDispatcher> _logger;

    public PacketDispatcher(IServiceProvider serviceProvider, ILogger<PacketDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async ValueTask DispatchAsync(PlayerSession session, PacketDefinition pktDef, byte[] payload, CancellationToken ct)
    {
        try
        {
            switch (pktDef.Name)
            {
                case "Walk":
                case "WalkOnCommand":
                case "WalkMinimap":
                    await HandleTypedAsync<WalkMessage>(session, DecodeWalk(payload), ct);
                    break;

                case "Command":
                    await HandleTypedAsync<CommandMessage>(session, DecodeCommand(payload), ct);
                    break;

                case "PublicChat":
                    await HandleTypedAsync<PublicChatMessage>(session, DecodePublicChat(payload), ct);
                    break;

                case "ButtonClick":
                    await HandleTypedAsync<ButtonClickMessage>(session, DecodeButtonClick(payload), ct);
                    break;

                case "EquipItem":
                    await HandleTypedAsync<EquipItemMessage>(session, DecodeEquipItem(payload), ct);
                    break;

                case "UnequipItem":
                    await HandleTypedAsync<UnequipItemMessage>(session, DecodeUnequipItem(payload), ct);
                    break;

                case "DropItem":
                    await HandleTypedAsync<DropItemMessage>(session, DecodeDropItem(payload), ct);
                    break;

                case "NpcInteract1":
                case "NpcInteract2":
                    int npcOpt = pktDef.Name == "NpcInteract1" ? 1 : 2;
                    await HandleTypedAsync<NpcInteractMessage>(session, DecodeNpcInteract(payload, npcOpt), ct);
                    break;

                case "ObjectInteract1":
                case "ObjectInteract2":
                    int objOpt = pktDef.Name == "ObjectInteract1" ? 1 : 2;
                    await HandleTypedAsync<ObjectInteractMessage>(session, DecodeObjectInteract(payload, objOpt), ct);
                    break;

                case "PlayerInteract1":
                case "PlayerInteract2":
                    int playerOpt = pktDef.Name == "PlayerInteract1" ? 1 : 2;
                    await HandleTypedAsync<PlayerInteractMessage>(session, DecodePlayerInteract(payload, playerOpt), ct);
                    break;

                case "GroundItemInteract":
                    await HandleTypedAsync<GroundItemInteractMessage>(session, DecodeGroundItemInteract(payload), ct);
                    break;

                case "DialogueContinue":
                    await HandleTypedAsync<DialogueContinueMessage>(session, DecodeDialogueContinue(payload), ct);
                    break;

                case "MoveItem":
                    await HandleTypedAsync<MoveItemMessage>(session, DecodeMoveItem(payload), ct);
                    break;

                case "CloseInterface":
                    await HandleTypedAsync<CloseInterfaceMessage>(session, new CloseInterfaceMessage(), ct);
                    break;

                case "KeepAlive":
                    await HandleTypedAsync<KeepAliveMessage>(session, new KeepAliveMessage(), ct);
                    break;

                case "RegionLoaded":
                    await HandleTypedAsync<RegionLoadedMessage>(session, new RegionLoadedMessage(), ct);
                    break;

                case "IdleLogout":
                    await HandleTypedAsync<IdleLogoutMessage>(session, new IdleLogoutMessage(), ct);
                    break;

                case "AppearanceUpdate":
                    await HandleTypedAsync<AppearanceUpdateMessage>(session, DecodeAppearanceUpdate(payload), ct);
                    break;

                case "FocusChanged":
                    await HandleTypedAsync<FocusChangedMessage>(session, 
                        new FocusChangedMessage(payload.Length > 0 && payload[0] == 1), ct);
                    break;

                case "AddFriend":
                    await HandleTypedAsync<AddFriendMessage>(session, DecodeAddFriend(payload), ct);
                    break;

                case "RemoveFriend":
                    await HandleTypedAsync<RemoveFriendMessage>(session, DecodeRemoveFriend(payload), ct);
                    break;

                case "AddIgnore":
                    await HandleTypedAsync<AddIgnoreMessage>(session, DecodeAddIgnore(payload), ct);
                    break;

                case "RemoveIgnore":
                    await HandleTypedAsync<RemoveIgnoreMessage>(session, DecodeRemoveIgnore(payload), ct);
                    break;

                case "PrivateMessage":
                    await HandleTypedAsync<PrivateMessageMessage>(session, DecodePrivateMessage(payload), ct);
                    break;

                case "CameraMoved":
                case "MouseClick":
                    // Ignore — anti-cheat telemetry
                    break;

                default:
                    _logger.LogTrace("Unhandled packet: {Name} (opcode={Opcode}, size={Size})",
                        pktDef.Name, pktDef.Opcode, payload.Length);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching packet {Name}", pktDef.Name);
        }
    }

    private async ValueTask HandleTypedAsync<T>(PlayerSession session, T message, CancellationToken ct) where T : struct
    {
        var handler = _serviceProvider.GetService(typeof(IMessageHandler<T>)) as IMessageHandler<T>;
        if (handler != null)
            await handler.HandleAsync(session, message, ct);
    }

    // --- Decoders: raw bytes → protocol-agnostic records ---

    private static WalkMessage DecodeWalk(byte[] data)
    {
        var reader = new PacketReader(data);
        int destX = reader.ReadShortA();
        int destY = reader.ReadLEShort();
        bool running = reader.ReadByteS() == 1;

        var steps = new List<WalkStep>();
        while (reader.Remaining >= 2)
        {
            int dx = reader.ReadByte();
            int dy = reader.ReadByte();
            steps.Add(new WalkStep(dx, dy));
        }

        return new WalkMessage(destX, destY, running, steps);
    }

    private static CommandMessage DecodeCommand(byte[] data)
    {
        var reader = new PacketReader(data);
        string full = reader.ReadString();
        var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new CommandMessage(
            parts.Length > 0 ? parts[0].ToLowerInvariant() : "",
            parts.Length > 1 ? parts[1..] : []
        );
    }

    private static PublicChatMessage DecodePublicChat(byte[] data)
    {
        var reader = new PacketReader(data);
        int color = reader.ReadByte();
        int effect = reader.ReadByte();
        var text = reader.ReadBytes(reader.Remaining).ToArray();
        return new PublicChatMessage(color, effect, text);
    }

    private static ButtonClickMessage DecodeButtonClick(byte[] data)
    {
        var reader = new PacketReader(data);
        int interfaceId = reader.ReadShort();
        int buttonId = reader.ReadShort();
        return new ButtonClickMessage(interfaceId, buttonId);
    }

    private static EquipItemMessage DecodeEquipItem(byte[] data)
    {
        var reader = new PacketReader(data);
        int itemId = reader.ReadShort();
        int slot = reader.ReadShortA();
        int interfaceId = reader.ReadShort();
        return new EquipItemMessage(itemId, slot, interfaceId);
    }

    private static UnequipItemMessage DecodeUnequipItem(byte[] data)
    {
        var reader = new PacketReader(data);
        int interfaceId = reader.ReadShort();
        int slot = reader.ReadShort();
        return new UnequipItemMessage(slot, interfaceId);
    }

    private static DropItemMessage DecodeDropItem(byte[] data)
    {
        var reader = new PacketReader(data);
        int itemId = reader.ReadShortA();
        int slot = reader.ReadShort();
        int interfaceId = reader.ReadShort();
        return new DropItemMessage(itemId, slot, interfaceId);
    }

    private static NpcInteractMessage DecodeNpcInteract(byte[] data, int option)
    {
        var reader = new PacketReader(data);
        int index = reader.ReadShort();
        return new NpcInteractMessage(index, option);
    }

    private static ObjectInteractMessage DecodeObjectInteract(byte[] data, int option)
    {
        var reader = new PacketReader(data);
        int objectId = reader.ReadShort();
        int x = reader.ReadShort();
        int y = reader.ReadShort();
        return new ObjectInteractMessage(objectId, x, y, option);
    }

    private static AppearanceUpdateMessage DecodeAppearanceUpdate(byte[] data)
    {
        var reader = new PacketReader(data);
        int gender = reader.ReadByte();
        var look = new int[7];
        for (int i = 0; i < 7; i++)
            look[i] = reader.ReadByte();
        var colors = new int[5];
        for (int i = 0; i < 5; i++)
            colors[i] = reader.ReadByte();
        return new AppearanceUpdateMessage(gender, look, colors);
    }

    private static PlayerInteractMessage DecodePlayerInteract(byte[] data, int option)
    {
        var reader = new PacketReader(data);
        int index = reader.ReadShort();
        return new PlayerInteractMessage(index, option);
    }

    private static GroundItemInteractMessage DecodeGroundItemInteract(byte[] data)
    {
        var reader = new PacketReader(data);
        int itemId = reader.ReadShort();
        int x = reader.ReadShort();
        int y = reader.ReadShort();
        return new GroundItemInteractMessage(itemId, x, y);
    }

    private static DialogueContinueMessage DecodeDialogueContinue(byte[] data)
    {
        var reader = new PacketReader(data);
        int interfaceId = reader.ReadShort();
        int buttonId = reader.ReadShort();
        return new DialogueContinueMessage(interfaceId, buttonId);
    }

    private static MoveItemMessage DecodeMoveItem(byte[] data)
    {
        var reader = new PacketReader(data);
        int interfaceId = reader.ReadShort();
        int fromSlot = reader.ReadShort();
        int toSlot = reader.ReadShort();
        return new MoveItemMessage(interfaceId, fromSlot, toSlot);
    }

    private static AddFriendMessage DecodeAddFriend(byte[] data)
    {
        var reader = new PacketReader(data);
        long nameLong = reader.ReadLong();
        return new AddFriendMessage(nameLong);
    }

    private static RemoveFriendMessage DecodeRemoveFriend(byte[] data)
    {
        var reader = new PacketReader(data);
        long nameLong = reader.ReadLong();
        return new RemoveFriendMessage(nameLong);
    }

    private static AddIgnoreMessage DecodeAddIgnore(byte[] data)
    {
        var reader = new PacketReader(data);
        long nameLong = reader.ReadLong();
        return new AddIgnoreMessage(nameLong);
    }

    private static RemoveIgnoreMessage DecodeRemoveIgnore(byte[] data)
    {
        var reader = new PacketReader(data);
        long nameLong = reader.ReadLong();
        return new RemoveIgnoreMessage(nameLong);
    }

    private static PrivateMessageMessage DecodePrivateMessage(byte[] data)
    {
        var reader = new PacketReader(data);
        long recipientLong = reader.ReadLong();
        var text = reader.ReadBytes(reader.Remaining).ToArray();
        return new PrivateMessageMessage(recipientLong, text);
    }
}
