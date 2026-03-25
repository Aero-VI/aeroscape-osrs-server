using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Pipeline;

/// <summary>
/// Decodes raw packet bytes into protocol-agnostic message records,
/// then resolves and invokes the appropriate IMessageHandler&lt;T&gt; from DI.
/// All packet names come from Protocol_508.json — no hardcoded opcodes.
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
                // ── Movement ──────────────────────────────────────────
                case "Walk":
                case "WalkOnCommand":
                case "WalkMinimap":
                    await HandleTypedAsync<WalkMessage>(session, DecodeWalk(pktDef.Name, payload), ct);
                    break;

                // ── Chat ──────────────────────────────────────────────
                case "Command":
                    await HandleTypedAsync<CommandMessage>(session, DecodeCommand(payload), ct);
                    break;

                case "PublicChat":
                    await HandleTypedAsync<PublicChatMessage>(session, DecodePublicChat(payload), ct);
                    break;

                // ── Button Clicks ─────────────────────────────────────
                case "ButtonClick1":
                case "ButtonClick2":
                case "ButtonClick3":
                case "ButtonClick4":
                case "ButtonClick5":
                case "ButtonClick6":
                    await HandleTypedAsync<ButtonClickMessage>(session, DecodeButtonClick(payload), ct);
                    break;

                // ── Equipment & Inventory ─────────────────────────────
                case "EquipItem":
                    await HandleTypedAsync<EquipItemMessage>(session, DecodeEquipItem(payload), ct);
                    break;

                case "DropItem":
                    await HandleTypedAsync<DropItemMessage>(session, DecodeDropItem(payload), ct);
                    break;

                case "MoveItem":
                    await HandleTypedAsync<MoveItemMessage>(session, DecodeMoveItem(payload), ct);
                    break;

                case "MoveItemExtended":
                    await HandleTypedAsync<SwitchItemExtendedMessage>(session, DecodeMoveItemExtended(payload), ct);
                    break;

                case "ItemOperate":
                    await HandleTypedAsync<ItemOperateMessage>(session, DecodeItemOperate(payload), ct);
                    break;

                case "ItemOption1":
                case "ItemOption1Alt":
                    await HandleTypedAsync<ItemOption1Message>(session, DecodeItemOption1(payload), ct);
                    break;

                case "ItemOption2":
                    await HandleTypedAsync<ItemOption2Message>(session, DecodeItemOption2(payload), ct);
                    break;

                case "ItemSelect":
                case "ItemSelectAlt":
                    await HandleTypedAsync<ItemSelectMessage>(session, DecodeItemSelect(payload), ct);
                    break;

                case "ItemOnItem":
                    await HandleTypedAsync<ItemOnItemMessage>(session, DecodeItemOnItem(payload), ct);
                    break;

                case "ItemOnNpc":
                    await HandleTypedAsync<ItemOnNpcMessage>(session, DecodeItemOnNpc(payload), ct);
                    break;

                case "ItemOnObject":
                    await HandleTypedAsync<ItemOnObjectMessage>(session, DecodeItemOnObject(payload), ct);
                    break;

                case "ItemOnPlayer":
                case "TradeAccept":
                    await HandleTypedAsync<ItemOnPlayerMessage>(session, DecodeItemOnPlayer(payload), ct);
                    break;

                // ── NPC Interaction ───────────────────────────────────
                case "NpcAttack":
                    await HandleTypedAsync<NpcAttackMessage>(session, DecodeNpcAttack(payload), ct);
                    break;

                case "NpcInteract1":
                case "NpcInteract2":
                case "NpcInteract3":
                    int npcOpt = pktDef.Name switch
                    {
                        "NpcInteract1" => 1,
                        "NpcInteract2" => 2,
                        "NpcInteract3" => 3,
                        _ => 1
                    };
                    await HandleTypedAsync<NpcInteractMessage>(session, DecodeNpcInteract(payload, npcOpt), ct);
                    break;

                // ── Object Interaction ────────────────────────────────
                case "ObjectInteract1":
                case "ObjectInteract2":
                case "ObjectBuild":
                    await HandleTypedAsync<ObjectInteractMessage>(session, DecodeObjectInteract(pktDef.Name, payload), ct);
                    break;

                // ── Player Interaction ────────────────────────────────
                case "PlayerInteract1":
                case "PlayerInteract2":
                case "PlayerInteract3":
                    int playerOpt = pktDef.Name switch
                    {
                        "PlayerInteract1" => 1,
                        "PlayerInteract2" => 2,
                        "PlayerInteract3" => 3,
                        _ => 1
                    };
                    await HandleTypedAsync<PlayerInteractMessage>(session, DecodePlayerInteract(payload, playerOpt), ct);
                    break;

                // ── Ground Item ───────────────────────────────────────
                case "GroundItemInteract":
                    await HandleTypedAsync<GroundItemInteractMessage>(session, DecodeGroundItemInteract(payload), ct);
                    break;

                // ── Magic ─────────────────────────────────────────────
                case "MagicOnNpc":
                    await HandleTypedAsync<MagicOnNpcMessage>(session, DecodeMagicOnNpc(payload), ct);
                    break;

                case "MagicOnPlayer":
                    await HandleTypedAsync<MagicOnPlayerMessage>(session, DecodeMagicOnPlayer(payload), ct);
                    break;

                case "MagicOnItem":
                    await HandleTypedAsync<MagicOnItemMessage>(session, DecodeMagicOnItem(payload), ct);
                    break;

                // ── Interface ─────────────────────────────────────────
                case "DialogueContinue":
                    await HandleTypedAsync<DialogueContinueMessage>(session, new DialogueContinueMessage(0, 0), ct);
                    break;

                case "CloseInterface":
                    await HandleTypedAsync<CloseInterfaceMessage>(session, new CloseInterfaceMessage(), ct);
                    break;

                case "AppearanceUpdate":
                    await HandleTypedAsync<AppearanceUpdateMessage>(session, DecodeAppearanceUpdate(payload), ct);
                    break;

                // ── Friends / Ignore ──────────────────────────────────
                case "AddFriend":
                    await HandleTypedAsync<AddFriendMessage>(session, new AddFriendMessage(ReadLong(payload)), ct);
                    break;

                case "RemoveFriend":
                    await HandleTypedAsync<RemoveFriendMessage>(session, new RemoveFriendMessage(ReadLong(payload)), ct);
                    break;

                case "AddIgnore":
                    await HandleTypedAsync<AddIgnoreMessage>(session, new AddIgnoreMessage(ReadLong(payload)), ct);
                    break;

                case "RemoveIgnore":
                    await HandleTypedAsync<RemoveIgnoreMessage>(session, new RemoveIgnoreMessage(ReadLong(payload)), ct);
                    break;

                case "PrivateMessage":
                    await HandleTypedAsync<PrivateMessageMessage>(session, DecodePrivateMessage(payload), ct);
                    break;

                // ── Clan Chat ─────────────────────────────────────────
                case "JoinClanChat":
                    await HandleTypedAsync<JoinClanChatMessage>(session, new JoinClanChatMessage(ReadLong(payload)), ct);
                    break;

                // ── Examine ───────────────────────────────────────────
                case "ExamineItem":
                    await HandleTypedAsync<ExamineItemMessage>(session, new ExamineItemMessage(ReadUShort(payload)), ct);
                    break;

                case "ExamineNpc":
                    await HandleTypedAsync<ExamineNpcMessage>(session, new ExamineNpcMessage(ReadUShort(payload)), ct);
                    break;

                case "ExamineObject":
                    await HandleTypedAsync<ExamineObjectMessage>(session, new ExamineObjectMessage(ReadUShort(payload)), ct);
                    break;

                // ── Input ─────────────────────────────────────────────
                case "NumberInput":
                    await HandleTypedAsync<NumberInputMessage>(session, new NumberInputMessage(ReadInt(payload)), ct);
                    break;

                case "StringInput":
                    await HandleTypedAsync<StringInputMessage>(session, DecodeStringInput(payload), ct);
                    break;

                case "LongInput":
                    await HandleTypedAsync<LongInputMessage>(session, new LongInputMessage(ReadLong(payload), 0), ct);
                    break;

                // ── Misc ──────────────────────────────────────────────
                case "KeepAlive":
                    await HandleTypedAsync<KeepAliveMessage>(session, new KeepAliveMessage(), ct);
                    break;

                case "RegionLoaded":
                case "RegionLoadedAlt":
                    await HandleTypedAsync<RegionLoadedMessage>(session, new RegionLoadedMessage(), ct);
                    break;

                case "IdleLogout":
                    await HandleTypedAsync<IdleLogoutMessage>(session, new IdleLogoutMessage(), ct);
                    break;

                case "FocusChanged":
                    await HandleTypedAsync<FocusChangedMessage>(session,
                        new FocusChangedMessage(payload.Length > 0 && payload[0] == 1), ct);
                    break;

                // Telemetry — silently ignore
                case "CameraMoved":
                case "SettingsButton":
                case "MouseClick":
                case "UpdateRequest":
                case "KickClanMember":
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

    // ═══════════════════════════════════════════════════════════════
    //  Inline decoders — raw bytes → protocol-agnostic records
    // ═══════════════════════════════════════════════════════════════

    private static long ReadLong(byte[] data) => new PacketReader(data).ReadLong();
    private static int ReadInt(byte[] data) => new PacketReader(data).ReadInt();
    private static int ReadUShort(byte[] data) => new PacketReader(data).ReadUShort();

    private static WalkMessage DecodeWalk(string packetName, byte[] data)
    {
        var reader = new PacketReader(data);
        int effectiveSize = packetName == "WalkOnCommand" ? data.Length - 14 : data.Length;
        int numPath = (effectiveSize - 5) / 2;

        int destX = reader.ReadUShort();
        int destY = reader.ReadShortA();
        bool running = reader.ReadByteC() == 1;

        var steps = new List<WalkStep>();
        for (int i = 0; i < numPath; i++)
        {
            int dx = reader.ReadSignedByte();
            int dy = reader.ReadByteS();
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
        int effects = reader.ReadUShort();
        int color = (effects >> 8) & 0xFF;
        int effect = effects & 0xFF;
        reader.ReadByte(); // numChars
        var text = reader.ReadBytes(reader.Remaining).ToArray();
        return new PublicChatMessage(color, effect, text);
    }

    private static ButtonClickMessage DecodeButtonClick(byte[] data)
    {
        var reader = new PacketReader(data);
        int interfaceHash = reader.ReadInt();
        int interfaceId = interfaceHash >> 16;
        int buttonId = interfaceHash & 0xFFFF;
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

    private static DropItemMessage DecodeDropItem(byte[] data)
    {
        var reader = new PacketReader(data);
        reader.ReadInt(); // junk
        int slot = reader.ReadUShort();
        int itemId = reader.ReadUShort();
        return new DropItemMessage(itemId, slot, 149);
    }

    private static MoveItemMessage DecodeMoveItem(byte[] data)
    {
        var reader = new PacketReader(data);
        int toSlot = reader.ReadUShort();
        reader.ReadByte();
        int fromSlot = reader.ReadUShort();
        reader.ReadUShort();
        int interfaceId = reader.ReadByte();
        reader.ReadByte();
        return new MoveItemMessage(interfaceId, fromSlot, toSlot);
    }

    private static SwitchItemExtendedMessage DecodeMoveItemExtended(byte[] data)
    {
        var reader = new PacketReader(data);
        int toHash = reader.ReadInt();
        int fromHash = reader.ReadInt();
        int fromSlot = reader.ReadUShort();
        int toSlot = reader.ReadUShort();
        return new SwitchItemExtendedMessage(fromSlot, toSlot, fromHash, toHash);
    }

    private static ItemOperateMessage DecodeItemOperate(byte[] data)
    {
        var reader = new PacketReader(data);
        int hash = reader.ReadInt();
        int itemId = reader.ReadShortA();
        int slot = reader.ReadUShort();
        return new ItemOperateMessage(itemId, slot, hash);
    }

    private static ItemOption1Message DecodeItemOption1(byte[] data)
    {
        var reader = new PacketReader(data);
        int slot = reader.ReadUShort();
        int interfaceId = reader.ReadUShort();
        reader.ReadUShort();
        int itemId = reader.ReadUShort();
        return new ItemOption1Message(itemId, slot, interfaceId);
    }

    private static ItemOption2Message DecodeItemOption2(byte[] data)
    {
        var reader = new PacketReader(data);
        int slot = reader.ReadUShort();
        int interfaceId = reader.ReadUShort();
        reader.ReadUShort();
        int itemId = reader.ReadUShort();
        return new ItemOption2Message(itemId, slot, interfaceId);
    }

    private static ItemSelectMessage DecodeItemSelect(byte[] data)
    {
        var reader = new PacketReader(data);
        int slot = reader.ReadUShort();
        int interfaceId = reader.ReadUShort();
        reader.ReadUShort();
        int itemId = reader.ReadUShort();
        return new ItemSelectMessage(itemId, slot, interfaceId);
    }

    private static ItemOnItemMessage DecodeItemOnItem(byte[] data)
    {
        var reader = new PacketReader(data);
        int usedWith = reader.ReadShort();
        int itemUsed = reader.ReadShortA();
        return new ItemOnItemMessage(itemUsed, usedWith);
    }

    private static ItemOnNpcMessage DecodeItemOnNpc(byte[] data)
    {
        var reader = new PacketReader(data);
        int itemId = reader.ReadShortA();
        int npcIndex = reader.ReadShort();
        return new ItemOnNpcMessage(itemId, npcIndex, 0, 0);
    }

    private static ItemOnObjectMessage DecodeItemOnObject(byte[] data)
    {
        var reader = new PacketReader(data);
        int objectId = reader.ReadUShort();
        int itemId = reader.ReadShortA();
        return new ItemOnObjectMessage(itemId, objectId, 0, 0);
    }

    private static ItemOnPlayerMessage DecodeItemOnPlayer(byte[] data)
    {
        var reader = new PacketReader(data);
        int targetIndex = reader.ReadUShort();
        return new ItemOnPlayerMessage(0, targetIndex);
    }

    private static NpcAttackMessage DecodeNpcAttack(byte[] data)
    {
        var reader = new PacketReader(data);
        int npcIndex = reader.ReadUShort();
        return new NpcAttackMessage(npcIndex);
    }

    private static NpcInteractMessage DecodeNpcInteract(byte[] data, int option)
    {
        var reader = new PacketReader(data);
        int index = reader.ReadUShort();
        return new NpcInteractMessage(index, option);
    }

    private static ObjectInteractMessage DecodeObjectInteract(string packetName, byte[] data)
    {
        var reader = new PacketReader(data);
        if (packetName == "ObjectInteract1")
        {
            int x = reader.ReadUShort();
            int objectId = reader.ReadUShort();
            int y = reader.ReadUShort();
            return new ObjectInteractMessage(objectId, x, y, 1);
        }
        else if (packetName == "ObjectBuild")
        {
            int y = reader.ReadUShort();
            int x = reader.ReadUShort();
            int objectId = reader.ReadUShort();
            return new ObjectInteractMessage(objectId, x, y, 3);
        }
        else
        {
            int objectId = reader.ReadUShort();
            return new ObjectInteractMessage(objectId, 0, 0, 2);
        }
    }

    private static PlayerInteractMessage DecodePlayerInteract(byte[] data, int option)
    {
        var reader = new PacketReader(data);
        int index = reader.ReadUShort();
        return new PlayerInteractMessage(index, option);
    }

    private static GroundItemInteractMessage DecodeGroundItemInteract(byte[] data)
    {
        var reader = new PacketReader(data);
        int y = reader.ReadUShort();
        int x = reader.ReadUShort();
        int itemId = reader.ReadUShort();
        return new GroundItemInteractMessage(itemId, x, y);
    }

    private static MagicOnNpcMessage DecodeMagicOnNpc(byte[] data)
    {
        var reader = new PacketReader(data);
        int npcIndex = reader.ReadShortA();
        int spellId = reader.ReadShortA();
        int interfaceId = reader.ReadUShort();
        return new MagicOnNpcMessage(npcIndex, spellId, interfaceId);
    }

    private static MagicOnPlayerMessage DecodeMagicOnPlayer(byte[] data)
    {
        var reader = new PacketReader(data);
        reader.ReadShortA(); // junk attack index
        int targetIndex = reader.ReadShort();
        int interfaceId = reader.ReadUShort();
        int spellId = reader.ReadUShort();
        return new MagicOnPlayerMessage(targetIndex, spellId, interfaceId);
    }

    private static MagicOnItemMessage DecodeMagicOnItem(byte[] data)
    {
        var reader = new PacketReader(data);
        int itemId = reader.ReadUShort();
        int slot = reader.ReadUShort();
        int spellId = reader.ReadUShort();
        int interfaceId = reader.ReadUShort();
        return new MagicOnItemMessage(itemId, slot, spellId, interfaceId);
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

    private static PrivateMessageMessage DecodePrivateMessage(byte[] data)
    {
        var reader = new PacketReader(data);
        long recipientLong = reader.ReadLong();
        reader.ReadByte(); // numChars
        var text = reader.ReadBytes(reader.Remaining).ToArray();
        return new PrivateMessageMessage(recipientLong, text);
    }

    private static StringInputMessage DecodeStringInput(byte[] data)
    {
        var reader = new PacketReader(data);
        return new StringInputMessage(reader.ReadString());
    }
}
